using System.Buffers.Binary;
using System.Numerics;

namespace Janitor.Acl;

/// <summary>
/// Decompresses uniformly sampled ACL 2.1 transform tracks into their exact samples, without interpolation.
/// </summary>
/// <param name="buffer">The compressed tracks buffer, starting at its raw buffer header.</param>
public sealed class AclTransformDecoder(byte[] buffer)
{
    private const int RawBitCount = 31;
    private const int TransformHeaderOffset = AclTracksHeader.Size;
    private const int SegmentHeaderSize = 16;
    private const byte DefaultSubTrack = 0;
    private const byte ConstantSubTrack = 1;
    private const byte AnimatedSubTrack = 2;

    private readonly AclTracksHeader _header = new(buffer);

    private bool _hasSegments;

    private int _formatOffset;

    private int _rangeOffset;

    private long _bitOffset;

    /// <summary>
    /// Decompresses every sample of every track.
    /// </summary>
    /// <returns>The decompressed tracks, in the order they were compressed.</returns>
    /// <exception cref="InvalidDataException">Thrown when the buffer does not contain compressed transform tracks.</exception>
    /// <exception cref="NotSupportedException">Thrown when the buffer uses a version or feature that is not supported.</exception>
    public AclTransformTrack[] Decompress()
    {
        _header.EnsureSupported(AclTrackType.Qvv);

        if (_header.HasDatabase || _header.HasStrippedKeyframes)
            throw new NotSupportedException("ACL databases and stripped keyframes are not supported.");

        var tracks = new AclTransformTrack[_header.TrackCount];

        for (var i = 0; i < tracks.Length; i++)
            tracks[i] = new AclTransformTrack();

        var rotationTypes = ReadSubTrackTypes(0);
        var translationTypes = ReadSubTrackTypes(1);
        var scaleTypes = _header.HasScale ? ReadSubTrackTypes(2) : new byte[tracks.Length];

        ReadDefaultSamples(tracks, rotationTypes, translationTypes, scaleTypes);
        ReadConstantSamples(tracks, rotationTypes, translationTypes, scaleTypes);
        ReadAnimatedSamples(tracks, rotationTypes, translationTypes, scaleTypes);

        return tracks;
    }

    private byte[] ReadSubTrackTypes(int subTrackKind)
    {
        var entriesPerKind = (_header.TrackCount + 15) / 16;
        var offset = TransformHeaderOffset + ReadInt32(TransformHeaderOffset + 40) + subTrackKind * entriesPerKind * 4;
        var types = new byte[_header.TrackCount];

        for (var i = 0; i < types.Length; i++)
            types[i] = (byte)((BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(offset + i / 16 * 4)) >> ((15 - i % 16) * 2)) & 3);

        return types;
    }

    private void ReadDefaultSamples(AclTransformTrack[] tracks, byte[] rotationTypes, byte[] translationTypes, byte[] scaleTypes)
    {
        if (!_header.HasTrivialDefaultValues)
            return;

        for (var i = 0; i < tracks.Length; i++)
        {
            if (rotationTypes[i] == DefaultSubTrack)
                tracks[i].Rotations = [Quaternion.Identity];
            if (translationTypes[i] == DefaultSubTrack)
                tracks[i].Translations = [Vector3.Zero];
            if (scaleTypes[i] == DefaultSubTrack)
                tracks[i].Scales = [new Vector3(_header.DefaultScale)];
        }
    }

    private void ReadConstantSamples(AclTransformTrack[] tracks, byte[] rotationTypes, byte[] translationTypes, byte[] scaleTypes)
    {
        var offset = TransformHeaderOffset + ReadInt32(TransformHeaderOffset + 44);
        var rotations = FindSubTracks(rotationTypes, ConstantSubTrack);

        if (_header.RotationFormat == AclRotationFormat.Full)
        {
            foreach (var track in rotations)
            {
                tracks[track].Rotations = [new Quaternion(ReadVector3(offset), ReadSingle(offset + 12))];
                offset += 16;
            }
        }
        else
        {
            for (var group = 0; group < rotations.Length; group += 4)
            {
                var groupSize = Math.Min(4, rotations.Length - group);

                for (var lane = 0; lane < groupSize; lane++)
                    tracks[rotations[group + lane]].Rotations = [CreateRotation(ReadSwizzledVector3(offset, groupSize, lane))];

                offset += groupSize * 12;
            }
        }

        foreach (var track in FindSubTracks(translationTypes, ConstantSubTrack))
        {
            tracks[track].Translations = [ReadVector3(offset)];
            offset += 12;
        }

        foreach (var track in FindSubTracks(scaleTypes, ConstantSubTrack))
        {
            tracks[track].Scales = [ReadVector3(offset)];
            offset += 12;
        }
    }

    private void ReadAnimatedSamples(AclTransformTrack[] tracks, byte[] rotationTypes, byte[] translationTypes, byte[] scaleTypes)
    {
        var rotations = FindSubTracks(rotationTypes, AnimatedSubTrack);
        var translations = FindSubTracks(translationTypes, AnimatedSubTrack);
        var scales = FindSubTracks(scaleTypes, AnimatedSubTrack);

        foreach (var track in rotations)
            tracks[track].Rotations = new Quaternion[_header.SampleCount];
        foreach (var track in translations)
            tracks[track].Translations = new Vector3[_header.SampleCount];
        foreach (var track in scales)
            tracks[track].Scales = new Vector3[_header.SampleCount];

        // Formats and segment ranges are laid out as rotations, padded to groups of 4, then translations and scales.
        var clipRangeOffset = TransformHeaderOffset + ReadInt32(TransformHeaderOffset + 48);
        var rotationRanges = ReadRotationClipRanges(rotations.Length, ref clipRangeOffset);
        var translationRanges = ReadVectorClipRanges(translations.Length, _header.TranslationFormat, ref clipRangeOffset);
        var scaleRanges = ReadVectorClipRanges(scales.Length, _header.ScaleFormat, ref clipRangeOffset);

        var rotationFormatCount = _header.RotationFormat == AclRotationFormat.DropWVariable ? (rotations.Length + 3) & ~3 : 0;
        var translationFormatCount = _header.TranslationFormat == AclVectorFormat.Variable ? translations.Length : 0;
        var segmentCount = ReadInt32(TransformHeaderOffset);
        var variableSubTrackCount = ReadInt32(TransformHeaderOffset + 4);
        var segmentHeadersOffset = TransformHeaderOffset + ReadInt32(TransformHeaderOffset + 36);

        _hasSegments = segmentCount > 1;

        for (var segment = 0; segment < segmentCount; segment++)
        {
            var segmentHeaderOffset = segmentHeadersOffset + segment * SegmentHeaderSize;
            var poseBitSize = ReadInt32(segmentHeaderOffset);
            var firstSample = _hasSegments ? ReadInt32(TransformHeaderOffset + 52 + segment * 4) : 0;
            var endSample = segment + 1 < segmentCount ? ReadInt32(TransformHeaderOffset + 56 + segment * 4) : _header.SampleCount;

            _formatOffset = TransformHeaderOffset + ReadInt32(segmentHeaderOffset + 12);
            _rangeOffset = Align(_formatOffset + variableSubTrackCount, 2);

            var dataOffset = Align(_rangeOffset + (_hasSegments ? variableSubTrackCount * 6 : 0), 4);

            for (var sample = firstSample; sample < endSample; sample++)
            {
                _bitOffset = dataOffset * 8L + (long)(sample - firstSample) * poseBitSize;

                for (var i = 0; i < rotations.Length; i++)
                    tracks[rotations[i]].Rotations[sample] = ReadAnimatedRotation(i, rotationRanges[i]);
                for (var i = 0; i < translations.Length; i++)
                    tracks[translations[i]].Translations[sample] = ReadAnimatedVector3(_header.TranslationFormat, rotationFormatCount + i, translationRanges[i]);
                for (var i = 0; i < scales.Length; i++)
                    tracks[scales[i]].Scales[sample] = ReadAnimatedVector3(_header.ScaleFormat, rotationFormatCount + translationFormatCount + i, scaleRanges[i]);
            }
        }
    }

    private (Vector3 Min, Vector3 Extent)[] ReadRotationClipRanges(int count, ref int offset)
    {
        var ranges = new (Vector3 Min, Vector3 Extent)[count];

        if (_header.RotationFormat != AclRotationFormat.DropWVariable)
            return ranges;

        for (var group = 0; group < count; group += 4)
        {
            var groupSize = Math.Min(4, count - group);

            for (var lane = 0; lane < groupSize; lane++)
                ranges[group + lane] = (ReadSwizzledVector3(offset, groupSize, lane), ReadSwizzledVector3(offset + groupSize * 12, groupSize, lane));

            offset += groupSize * 24;
        }

        return ranges;
    }

    private (Vector3 Min, Vector3 Extent)[] ReadVectorClipRanges(int count, AclVectorFormat format, ref int offset)
    {
        var ranges = new (Vector3 Min, Vector3 Extent)[count];

        if (format != AclVectorFormat.Variable)
            return ranges;

        for (var i = 0; i < count; i++)
        {
            ranges[i] = (ReadVector3(offset), ReadVector3(offset + 12));
            offset += 24;
        }

        return ranges;
    }

    private Quaternion ReadAnimatedRotation(int index, (Vector3 Min, Vector3 Extent) clipRange)
    {
        if (_header.RotationFormat == AclRotationFormat.Full)
            return new Quaternion(ReadRawVector3(), ReadRawSingle());
        if (_header.RotationFormat == AclRotationFormat.DropWFull)
            return CreateRotation(ReadRawVector3());

        var bitCount = buffer[_formatOffset + index];

        if (bitCount == RawBitCount)
            return CreateRotation(ReadRawVector3());

        // Segment ranges of rotations are swizzled into groups of 4 as min XXXX, YYYY, ZZZZ followed by extent XXXX, YYYY, ZZZZ.
        // Constant sub-tracks store their 16 bit sample within the range instead.
        var rangeOffset = _rangeOffset + index / 4 * 24 + index % 4;
        Vector3 value;

        if (bitCount == 0)
        {
            value = new Vector3(buffer[rangeOffset] << 8 | buffer[rangeOffset + 4], buffer[rangeOffset + 8] << 8 | buffer[rangeOffset + 12], buffer[rangeOffset + 16] << 8 | buffer[rangeOffset + 20]) * (1.0f / ushort.MaxValue);
        }
        else
        {
            value = ReadQuantizedVector3(bitCount);

            if (_hasSegments)
                value = value * (new Vector3(buffer[rangeOffset + 12], buffer[rangeOffset + 16], buffer[rangeOffset + 20]) * (1.0f / byte.MaxValue)) + new Vector3(buffer[rangeOffset], buffer[rangeOffset + 4], buffer[rangeOffset + 8]) * (1.0f / byte.MaxValue);
        }

        return CreateRotation(value * clipRange.Extent + clipRange.Min);
    }

    private Vector3 ReadAnimatedVector3(AclVectorFormat format, int index, (Vector3 Min, Vector3 Extent) clipRange)
    {
        if (format == AclVectorFormat.Full)
            return ReadRawVector3();

        var bitCount = buffer[_formatOffset + index];

        if (bitCount == RawBitCount)
            return ReadRawVector3();

        // Segment ranges of vectors are stored as min XYZ followed by extent XYZ, constant sub-tracks store their 16 bit sample within the range instead.
        var rangeOffset = _rangeOffset + index * 6;
        Vector3 value;

        if (bitCount == 0)
        {
            value = new Vector3(ReadUInt16(rangeOffset), ReadUInt16(rangeOffset + 2), ReadUInt16(rangeOffset + 4)) * (1.0f / ushort.MaxValue);
        }
        else
        {
            value = ReadQuantizedVector3(bitCount);

            if (_hasSegments)
                value = value * (new Vector3(buffer[rangeOffset + 3], buffer[rangeOffset + 4], buffer[rangeOffset + 5]) * (1.0f / byte.MaxValue)) + new Vector3(buffer[rangeOffset], buffer[rangeOffset + 1], buffer[rangeOffset + 2]) * (1.0f / byte.MaxValue);
        }

        return value * clipRange.Extent + clipRange.Min;
    }

    private Vector3 ReadQuantizedVector3(int bitCount) => new(ReadNormalized(bitCount), ReadNormalized(bitCount), ReadNormalized(bitCount));

    private Vector3 ReadRawVector3() => new(ReadRawSingle(), ReadRawSingle(), ReadRawSingle());

    private float ReadNormalized(int bitCount)
    {
        var value = AclBitStream.ReadNormalized(buffer, _bitOffset, bitCount);

        _bitOffset += bitCount;

        return value;
    }

    private float ReadRawSingle()
    {
        var value = AclBitStream.ReadSingle(buffer, _bitOffset);

        _bitOffset += 32;

        return value;
    }

    private ushort ReadUInt16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(offset));

    private int ReadInt32(int offset) => BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(offset));

    private float ReadSingle(int offset) => BinaryPrimitives.ReadSingleLittleEndian(buffer.AsSpan(offset));

    private Vector3 ReadVector3(int offset) => new(ReadSingle(offset), ReadSingle(offset + 4), ReadSingle(offset + 8));

    // Rotation data is swizzled into groups of up to 4 as XXXX, YYYY, ZZZZ.
    private Vector3 ReadSwizzledVector3(int offset, int groupSize, int lane) => new(ReadSingle(offset + lane * 4), ReadSingle(offset + (groupSize + lane) * 4), ReadSingle(offset + (groupSize * 2 + lane) * 4));

    private static Quaternion CreateRotation(Vector3 xyz) => Quaternion.Normalize(new Quaternion(xyz, MathF.Sqrt(MathF.Abs(1.0f - xyz.X * xyz.X - xyz.Y * xyz.Y - xyz.Z * xyz.Z))));

    private static int[] FindSubTracks(byte[] types, byte type) => [.. Enumerable.Range(0, types.Length).Where(i => types[i] == type)];

    private static int Align(int offset, int alignment) => (offset + alignment - 1) & -alignment;
}
