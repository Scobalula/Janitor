using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Janitor.Wwise;

/// <summary>
/// Bindings for the multistream decoder of libopus (opus.dll), which also decodes plain mono and stereo streams.
/// </summary>
internal static unsafe partial class OpusInterop
{
    private const string Library = "Native/opus";

    public const int Success = 0;

    [LibraryImport(Library, EntryPoint = "opus_multistream_decoder_create")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint CreateDecoder(int sampleRate, int channels, int streams, int coupledStreams, byte* mapping, int* error);

    [LibraryImport(Library, EntryPoint = "opus_multistream_decoder_destroy")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void DestroyDecoder(nint decoder);

    [LibraryImport(Library, EntryPoint = "opus_multistream_decode")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int Decode(nint decoder, byte* data, int length, short* pcm, int frameSize, int decodeForwardErrorCorrection);
}
