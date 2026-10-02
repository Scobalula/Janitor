using Janitor.Acl;
using Janitor.Pack2FileSystem;
using RedFox.GameExtraction;
using RedFox.GameExtraction.AssetHandlers;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Skeletal;
using RedFox.IO;
using System.Numerics;

namespace Janitor.AssetHandling;

/// <summary>
/// Handles puppet::ClipResource (.binanimclip) assets, reading their ACL compressed bone tracks, curves, events, sections and
/// bone visibility into a skeletal animation.
/// </summary>
public class ClipResourceHandler : AnimationHandler
{
    private const float PositionScale = 100.0f;

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
        // * Bones are only referenced by the FNV-1a hash of their lower case name, names are resolved from the "BoneTable" name table.
        // * The trajectory is the root motion of the clip, the first bone is the skeleton root and is relative to it, so we apply it there.
        // * Curves are named after Maya attributes (node.attribute), so each becomes a custom curve of the track named after its node.
        // * Events are curves too, but only hold instants or ranges, so they are stored as notes along with the section boundaries.
        // * The distance travelled along the trajectory per frame (0x28) is skipped, it is derived from the trajectory.
        cancellationToken.ThrowIfCancellationRequested();

        if (asset.Source is not Pack2Source)
            throw new NotSupportedException("Only Pack2Source assets are supported.");
        if (asset.DataSource is not Pack2File file)
            throw new NotSupportedException("Only Pack2File data sources are supported.");

        using var reader = new BinaryReader(file.Open());

        reader.BaseStream.Position = (long)(((ulong)reader.BaseStream.Position + 19) & 0xFFFFFFFFFFFFFFF0);

        var clipOffset = reader.BaseStream.Position + reader.ReadUInt32();
        var compression = reader.ReadByte(clipOffset + 13);

        if (compression is not (1 or 2))
            throw new NotSupportedException($"Clip compression type {compression}");

        var animation = new SkeletonAnimation(Path.GetFileNameWithoutExtension(asset.Name), reader.ReadUInt16(clipOffset + 4), TransformType.Absolute)
        {
            Framerate = reader.ReadStruct<float>(clipOffset + 24),
            TransformSpace = TransformSpace.Local,
        };

        context.TryGetService<NameListService>(out var nameList);

        var curveNames = ReadCurveNames(reader, clipOffset);

        ReadBoneTracks(reader, clipOffset, animation, nameList);
        ReadBoneVisibility(reader, clipOffset, animation);
        ReadCurves(reader, clipOffset, animation, curveNames);
        ReadEvents(reader, clipOffset, animation, curveNames);
        ReadSections(reader, clipOffset, animation);

        var scene = new Scene(animation.Name);

        scene.AddNode(animation);

        return Task.FromResult(new AssetReadResult
        {
            Asset = asset,
            Handler = this,
            Data = scene,
        });
    }

    private static void ReadBoneTracks(BinaryReader reader, long clipOffset, SkeletonAnimation animation, NameListService? nameList)
    {
        var frameCount = reader.ReadInt32(clipOffset);
        var boneCount = reader.ReadUInt16(clipOffset + 4);
        var boneHashes = reader.ReadStructArray<uint>(boneCount, ReadRelativeOffset(reader, clipOffset + 100)).ToArray();
        var boneTracks = new AclTransformDecoder(ReadCompressedTracks(reader, clipOffset, clipOffset + 32)).Decompress();

        if (reader.ReadInt32(clipOffset + 36) != 0)
            ApplyTrajectory(boneTracks[0], new AclTransformDecoder(ReadCompressedTracks(reader, clipOffset, clipOffset + 36)).Decompress()[0], frameCount);

        for (var i = 0; i < boneCount; i++)
        {
            var name = nameList is not null && nameList.Manager.TryGetValue("BoneTable", boneHashes[i], out var boneName) ? boneName : $"bone_{boneHashes[i]}";

            animation.Tracks.Add(CreateTrack(name, boneTracks[i]));
        }
    }

    private static void ReadBoneVisibility(BinaryReader reader, long clipOffset, SkeletonAnimation animation)
    {
        // Bones are hidden in groups, each frame stores a bit per group that is cleared while its bones are hidden.
        var groupCount = reader.ReadInt32(clipOffset + 16);

        if (groupCount == 0)
            return;

        var frameCount = reader.ReadInt32(clipOffset);
        var wordsPerFrame = (groupCount >> 6) + 1;
        var visibilityBits = reader.ReadStructArray<ulong>(frameCount * wordsPerFrame, ReadRelativeOffset(reader, clipOffset + 72)).ToArray();
        var groupBoneCounts = reader.ReadStructArray<ushort>(groupCount, ReadRelativeOffset(reader, clipOffset + 80)).ToArray();
        var groupBones = reader.ReadStructArray<ushort>(groupBoneCounts.Sum(x => x), ReadRelativeOffset(reader, clipOffset + 76)).ToArray();
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

    private static void ReadCurves(BinaryReader reader, long clipOffset, SkeletonAnimation animation, string[] curveNames)
    {
        var compressedCurveCount = reader.ReadByte(clipOffset + 12);

        if (compressedCurveCount == 0)
            return;

        var curveIndices = reader.ReadBytes(compressedCurveCount, ReadRelativeOffset(reader, clipOffset + 52));
        var curveSamples = new AclScalarDecoder(ReadCompressedTracks(reader, clipOffset, clipOffset + 56)).Decompress();

        for (var i = 0; i < compressedCurveCount; i++)
        {
            var name = curveNames[curveIndices[i]];
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

    private static void ReadEvents(BinaryReader reader, long clipOffset, SkeletonAnimation animation, string[] curveNames)
    {
        var frameCount = reader.ReadInt32(clipOffset);
        var instantCount = reader.ReadUInt16(clipOffset + 8);
        var rangeCount = reader.ReadUInt16(clipOffset + 10);
        var instantOffset = ReadRelativeOffset(reader, clipOffset + 60);
        var rangeOffset = ReadRelativeOffset(reader, clipOffset + 64);

        for (var i = 0; i < instantCount; i++)
            AddNote(animation, curveNames[reader.ReadInt32(instantOffset + i * 8)], reader.ReadStruct<float>(instantOffset + i * 8 + 4), frameCount);

        for (var i = 0; i < rangeCount; i++)
        {
            var name = curveNames[reader.ReadInt32(rangeOffset + i * 12)];

            AddNote(animation, $"{name}_start", reader.ReadStruct<float>(rangeOffset + i * 12 + 4), frameCount);
            AddNote(animation, $"{name}_end", reader.ReadStruct<float>(rangeOffset + i * 12 + 8), frameCount);
        }
    }

    private static void ReadSections(BinaryReader reader, long clipOffset, SkeletonAnimation animation)
    {
        // Sections split the clip by their boundary times, the first and last boundary being the start and end of the clip.
        var frameCount = reader.ReadInt32(clipOffset);
        var sectionCount = reader.ReadInt32(clipOffset + 92);
        var boundaries = reader.ReadStructArray<float>(sectionCount + 1, ReadRelativeOffset(reader, clipOffset + 96)).ToArray();

        for (var i = 1; i < sectionCount; i++)
            AddNote(animation, "section", boundaries[i], frameCount);
    }

    private static string[] ReadCurveNames(BinaryReader reader, long clipOffset)
    {
        var curveCount = reader.ReadUInt16(clipOffset + 6);
        var curveHashes = reader.ReadStructArray<uint>(curveCount, ReadRelativeOffset(reader, clipOffset + 44)).ToArray();
        var namesOffset = reader.ReadInt64(clipOffset + 104);

        if (namesOffset == 0)
            return [.. curveHashes.Select(x => $"curve_{x}")];

        return [.. Enumerable.Range(0, curveCount).Select(i => reader.ReadUTF8NullTerminatedString(clipOffset + reader.ReadInt64(clipOffset + namesOffset + i * 8)))];
    }

    private static void AddNote(SkeletonAnimation animation, string name, float time, int frameCount)
    {
        var frame = Math.Clamp(MathF.Round(time * animation.Framerate), 0, frameCount - 1);

        animation.CreateAction(name).KeyFrames.Add(new(frame, null));
    }

    private static SkeletonAnimationTrack CreateTrack(string name, AclTransformTrack source)
    {
        var track = new SkeletonAnimationTrack(name)
        {
            TransformSpace = TransformSpace.Local,
            TransformType = TransformType.Absolute,
        };

        for (var frame = 0; frame < source.Rotations.Length; frame++)
            track.AddRotationFrame(frame, source.Rotations[frame]);
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

    private static byte[] ReadCompressedTracks(BinaryReader reader, long clipOffset, long fieldOffset)
    {
        var tracksOffset = clipOffset + reader.ReadInt64(ReadRelativeOffset(reader, fieldOffset));

        return reader.ReadBytes(reader.ReadInt32(tracksOffset), tracksOffset);
    }

    private static long ReadRelativeOffset(BinaryReader reader, long offset) => offset + reader.ReadInt32(offset);
}
