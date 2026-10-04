using Janitor.Acl;
using Janitor.Pack2FileSystem;
using Janitor.Animation;
using Microsoft.Extensions.Logging;
using RedFox.GameExtraction;
using RedFox.GameExtraction.AssetHandlers;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Skeletal;
using RedFox.IO;
using System.Numerics;

namespace Janitor.AssetHandling;

/// <summary>
/// Handles animation clip (.binanimclip) assets, reading their raw or ACL compressed bone tracks, curves, events, sections and
/// bone visibility into a skeletal animation.
/// </summary>
public class ClipResourceHandler : AnimationHandler
{
    private const float PositionScale = 100.0f;

    private const int RelocationInfoOffset = 16;

    private const int RawCompression = 0;

    private const string TrajectoryTrackName = "trajectory";

    /// <inheritdoc/>
    public override bool CanHandle(Asset asset, GameExtractionConfiguration configuration)
    {
        if (asset.Source is not Pack2Source)
            return false;
        if (asset.DataSource is not Pack2File)
            return false;
        if (!asset.Name.EndsWith(".binanimclip", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    /// <inheritdoc/>
    public override Task<AssetReadResult> ReadAsync(Asset asset, AssetReadContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (asset.Source is not Pack2Source)
            throw new NotSupportedException("Only Pack2Source assets are supported.");
        if (asset.DataSource is not Pack2File file)
            throw new NotSupportedException("Only Pack2File data sources are supported.");

        context.TryGetService<NameListService>(out var nameList);

        var animation = ReadAnimation(file, nameList, null);
        var scene = new Scene(animation.Name);

        scene.AddNode(animation);

        context.AssetManager.Logger.LogDebug("Read clip {Clip} with {TrackCount} tracks", animation.Name, animation.Tracks.Count);

        return Task.FromResult(new AssetReadResult
        {
            Asset = asset,
            Handler = this,
            Data = scene,
        });
    }

    /// <summary>
    /// Reads a clip into a skeletal animation, relative to the base pose of a graph that samples it as an additive layer.
    /// </summary>
    /// <param name="file">The clip file.</param>
    /// <param name="nameList">The name tables bone names are resolved from, if any.</param>
    /// <param name="additiveClip">The additive use of the clip, or <see langword="null"/> to read its absolute transforms.</param>
    /// <returns>The animation.</returns>
    /// <exception cref="NotSupportedException">Thrown when the clip version or compression is not supported.</exception>
    public static SkeletonAnimation ReadAnimation(Pack2File file, NameListService? nameList, AdditiveClip? additiveClip)
    {
        // * Bones are only referenced by the FNV-1a hash of their lower case name, names are resolved from the "BoneTable" name table.
        // * The trajectory is the root motion of the clip, the first bone is the skeleton root and is relative to it, so we apply it there.
        //   Spline clips have no bones, so their trajectory becomes a track of its own.
        // * Curves are named after Maya attributes (node.attribute), so each becomes a custom curve of the track named after its node.
        // * Events are curves too, but only hold instants or ranges, so they are stored as notes along with the section boundaries.
        // * The distance travelled along the trajectory per frame (0x28) is skipped, it is derived from the trajectory.
        // * Additive clips have the graph's inverse base pose applied, as the game does when sampling them, and leave out the trajectory.
        using var reader = new BinaryReader(file.Open());

        var header = new ClipHeader(reader, RelocationInfoOffset + reader.ReadUInt32(RelocationInfoOffset), reader.ReadInt32(0));

        if (header.Compression is not (RawCompression or 1 or 2))
            throw new NotSupportedException($"Clip compression type {header.Compression}");

        var animation = new SkeletonAnimation(Path.GetFileNameWithoutExtension(file.Name), header.BoneCount, additiveClip is null ? TransformType.Absolute : TransformType.Additive)
        {
            Framerate = header.Framerate,
            TransformSpace = TransformSpace.Local,
        };

        var curveNames = ReadCurveNames(reader, header);

        ReadBoneTracks(reader, header, animation, nameList, additiveClip);
        ReadBoneVisibility(reader, header, animation);
        ReadCurves(reader, header, animation, curveNames);
        ReadEvents(reader, header, animation, curveNames);
        ReadSections(reader, header, animation);

        return animation;
    }

    private static void ReadBoneTracks(BinaryReader reader, ClipHeader header, SkeletonAnimation animation, NameListService? nameList, AdditiveClip? additiveClip)
    {
        var boneHashes = reader.ReadStructArray<uint>(header.BoneCount, ReadRelativeOffset(reader, header.BoneHashesField)).ToArray();
        var boneTracks = reader.ReadInt32(header.BoneTracksField) == 0 ? [] : ReadTransformTracks(reader, header, header.BoneTracksField, header.BoneCount);
        var trajectory = reader.ReadInt32(header.TrajectoryField) == 0 ? null : ReadTransformTracks(reader, header, header.TrajectoryField, 1)[0];

        if (trajectory is not null && boneTracks.Length > 0 && additiveClip is null)
            ApplyTrajectory(boneTracks[0], trajectory, header.FrameCount);
        if (trajectory is not null && boneTracks.Length == 0)
            animation.Tracks.Add(CreateTrack(TrajectoryTrackName, trajectory, TransformType.Absolute));

        for (var i = 0; i < header.BoneCount; i++)
        {
            var name = nameList is not null && nameList.Manager.TryGetValue("BoneTable", boneHashes[i], out var boneName) ? boneName : $"bone_{boneHashes[i]}";

            // Bones outside the graph's rig are not layered by the graph, so they keep their absolute transforms.
            (Quaternion Rotation, Vector3 Translation) inverseBasePose = default;
            var isAdditive = additiveClip?.InverseBasePose.TryGetValue(boneHashes[i], out inverseBasePose) == true;

            if (isAdditive)
                SubtractBasePose(boneTracks[i], inverseBasePose);

            animation.Tracks.Add(CreateTrack(name, boneTracks[i], isAdditive ? TransformType.Additive : TransformType.Absolute));
        }
    }

    private static void ReadBoneVisibility(BinaryReader reader, ClipHeader header, SkeletonAnimation animation)
    {
        // Bones are hidden in groups, each frame stores a bit per group that is cleared while its bones are hidden.
        var groupCount = header.VisibilityGroupCount;

        if (groupCount == 0)
            return;

        var frameCount = header.FrameCount;
        var wordsPerFrame = (groupCount >> 6) + 1;
        var visibilityBits = reader.ReadStructArray<ulong>(frameCount * wordsPerFrame, ReadRelativeOffset(reader, header.VisibilityBitsField)).ToArray();
        var groupBoneCounts = reader.ReadStructArray<ushort>(groupCount, ReadRelativeOffset(reader, header.VisibilityBoneCountsField)).ToArray();
        var groupBones = reader.ReadStructArray<ushort>(groupBoneCounts.Sum(x => x), ReadRelativeOffset(reader, header.VisibilityBonesField)).ToArray();
        var visibility = new Dictionary<int, bool[]>();
        var firstBone = 0;

        for (var group = 0; group < groupCount; group++)
        {
            foreach (var bone in groupBones.AsSpan(firstBone, groupBoneCounts[group]))
            {
                if (!visibility.TryGetValue(bone, out var visible))
                    visibility[bone] = visible = [.. Enumerable.Repeat(true, frameCount)];

                for (var frame = 0; frame < frameCount; frame++)
                    visible[frame] &= (visibilityBits[frame * wordsPerFrame + (group >> 6)] & (1UL << (group & 63))) != 0;
            }

            firstBone += groupBoneCounts[group];
        }

        foreach (var (bone, visible) in visibility)
        {
            var curve = animation.Tracks[bone].GetOrCreateCustomCurve("visibility", 1);

            for (var frame = 0; frame < frameCount; frame++)
                curve.Add(frame, visible[frame] ? 1.0f : 0.0f);
        }
    }

    private static void ReadCurves(BinaryReader reader, ClipHeader header, SkeletonAnimation animation, string[] curveNames)
    {
        if (header.CompressedCurveCount == 0)
            return;

        var curveIndicesOffset = ReadRelativeOffset(reader, header.CurveIndicesField);
        var curveSamples = ReadCurveSamples(reader, header);

        for (var i = 0; i < header.CompressedCurveCount; i++)
        {
            var name = curveNames[header.CurveIndexSize == 1 ? reader.ReadByte(curveIndicesOffset + i) : reader.ReadUInt16(curveIndicesOffset + i * 2)];
            var separator = name.LastIndexOf('.');
            var nodeName = separator < 0 ? name : name[..separator];
            var track = animation.Tracks.Find(x => x.Name == nodeName);

            if (track is null)
            {
                track = new SkeletonAnimationTrack(nodeName);
                animation.Tracks.Add(track);
            }

            var curve = track.GetOrCreateCustomCurve(separator < 0 ? "value" : name[(separator + 1)..], 1);

            for (var frame = 0; frame < curveSamples[i].Length; frame++)
                curve.Add(frame, curveSamples[i][frame]);
        }
    }

    private static void ReadEvents(BinaryReader reader, ClipHeader header, SkeletonAnimation animation, string[] curveNames)
    {
        var instantOffset = ReadRelativeOffset(reader, header.InstantEventsField);
        var rangeOffset = ReadRelativeOffset(reader, header.RangeEventsField);

        for (var i = 0; i < header.InstantEventCount; i++)
            AddNote(animation, curveNames[reader.ReadInt32(instantOffset + i * 8)], reader.ReadStruct<float>(instantOffset + i * 8 + 4), header.FrameCount);

        for (var i = 0; i < header.RangeEventCount; i++)
        {
            var name = curveNames[reader.ReadInt32(rangeOffset + i * 12)];

            AddNote(animation, $"{name}_start", reader.ReadStruct<float>(rangeOffset + i * 12 + 4), header.FrameCount);
            AddNote(animation, $"{name}_end", reader.ReadStruct<float>(rangeOffset + i * 12 + 8), header.FrameCount);
        }
    }

    private static void ReadSections(BinaryReader reader, ClipHeader header, SkeletonAnimation animation)
    {
        // Sections split the clip by their boundaries, the first and last boundary being the start and end of the clip.
        var boundaries = reader.ReadStructArray<float>(header.SectionCount + 1, ReadRelativeOffset(reader, header.SectionsField)).ToArray();
        var timeScale = header.SectionsAreFractions ? header.Duration : 1.0f;

        for (var i = 1; i < header.SectionCount; i++)
            AddNote(animation, "section", boundaries[i] * timeScale, header.FrameCount);
    }

    private static string[] ReadCurveNames(BinaryReader reader, ClipHeader header)
    {
        if (header.CurveNamesOffset == 0)
            return [.. reader.ReadStructArray<uint>(header.CurveCount, ReadRelativeOffset(reader, header.CurveHashesField)).ToArray().Select(x => $"curve_{x}")];

        return [.. Enumerable.Range(0, header.CurveCount).Select(i => reader.ReadUTF8NullTerminatedString(header.Offset + reader.ReadInt64(header.Offset + header.CurveNamesOffset + i * 8)))];
    }

    private static void AddNote(SkeletonAnimation animation, string name, float time, int frameCount)
    {
        var frame = Math.Clamp(MathF.Round(time * animation.Framerate), 0, frameCount - 1);

        animation.CreateAction(name).KeyFrames.Add(new(frame, null));
    }

    private static void SubtractBasePose(AclTransformTrack track, (Quaternion Rotation, Vector3 Translation) inverseBasePose)
    {
        // The additive transforms the game layers carry no scale.
        track.Rotations = [.. track.Rotations.Select(x => Quaternion.Multiply(inverseBasePose.Rotation, x))];
        track.Translations = [.. track.Translations.Select(x => x + inverseBasePose.Translation)];
        track.Scales = [];
    }

    private static SkeletonAnimationTrack CreateTrack(string name, AclTransformTrack source, TransformType transformType)
    {
        var track = new SkeletonAnimationTrack(name)
        {
            TransformSpace = TransformSpace.Local,
            TransformType = transformType,
        };

        // Rotations are kept in the hemisphere of the previous frame, so interpolating between keys takes the shortest path as the game does.
        for (var frame = 0; frame < source.Rotations.Length; frame++)
        {
            if (frame > 0 && Quaternion.Dot(source.Rotations[frame - 1], source.Rotations[frame]) < 0)
                source.Rotations[frame] = Quaternion.Negate(source.Rotations[frame]);

            track.AddRotationFrame(frame, source.Rotations[frame]);
        }

        for (var frame = 0; frame < source.Translations.Length; frame++)
            track.AddTranslationFrame(frame, source.Translations[frame] * PositionScale);
        for (var frame = 0; frame < source.Scales.Length; frame++)
            track.AddScaleFrame(frame, source.Scales[frame]);

        return track;
    }

    private static void ApplyTrajectory(AclTransformTrack root, AclTransformTrack trajectory, int frameCount)
    {
        var rotations = new Quaternion[frameCount];
        var translations = new Vector3[frameCount];

        for (var frame = 0; frame < frameCount; frame++)
        {
            var trajectoryRotation = Sample(trajectory.Rotations, frame, Quaternion.Identity);

            rotations[frame] = trajectoryRotation * Sample(root.Rotations, frame, Quaternion.Identity);
            translations[frame] = Sample(trajectory.Translations, frame, Vector3.Zero) + Vector3.Transform(Sample(root.Translations, frame, Vector3.Zero), trajectoryRotation);
        }

        root.Rotations = rotations;
        root.Translations = translations;
    }

    private static T Sample<T>(T[] samples, int frame, T fallback) => samples.Length == 0 ? fallback : samples[Math.Min(frame, samples.Length - 1)];

    private static AclTransformTrack[] ReadTransformTracks(BinaryReader reader, ClipHeader header, long field, int trackCount)
    {
        // Raw transforms are stored per frame as a rotation quaternion followed by a translation padded to 16 bytes, without scale.
        var dataOffset = ReadDataOffset(reader, header, field);

        if (header.Compression != RawCompression)
            return new AclTransformDecoder(ReadAclBuffer(reader, dataOffset)).Decompress();

        var tracks = new AclTransformTrack[trackCount];

        for (var track = 0; track < trackCount; track++)
        {
            tracks[track] = new AclTransformTrack
            {
                Rotations = new Quaternion[header.FrameCount],
                Translations = new Vector3[header.FrameCount],
            };

            for (var frame = 0; frame < header.FrameCount; frame++)
            {
                var sampleOffset = dataOffset + (frame * trackCount + track) * 32L;

                tracks[track].Rotations[frame] = reader.ReadStruct<Quaternion>(sampleOffset);
                tracks[track].Translations[frame] = reader.ReadStruct<Vector3>(sampleOffset + 16);
            }
        }

        return tracks;
    }

    private static float[][] ReadCurveSamples(BinaryReader reader, ClipHeader header)
    {
        // Raw curves are stored per frame as a float per sampled curve.
        var dataOffset = ReadDataOffset(reader, header, header.CurvesField);

        if (header.Compression != RawCompression)
            return new AclScalarDecoder(ReadAclBuffer(reader, dataOffset)).Decompress();

        var samples = reader.ReadStructArray<float>(header.FrameCount * header.CompressedCurveCount, dataOffset).ToArray();

        return [.. Enumerable.Range(0, header.CompressedCurveCount).Select(curve => Enumerable.Range(0, header.FrameCount).Select(frame => samples[frame * header.CompressedCurveCount + curve]).ToArray())];
    }

    private static long ReadDataOffset(BinaryReader reader, ClipHeader header, long field) => header.Offset + reader.ReadInt64(ReadRelativeOffset(reader, field));

    private static byte[] ReadAclBuffer(BinaryReader reader, long offset) => reader.ReadBytes(reader.ReadInt32(offset), offset);

    private static long ReadRelativeOffset(BinaryReader reader, long offset) => offset + reader.ReadInt32(offset);
}
