using System.Runtime.InteropServices;

namespace Janitor.Wwise;

/// <summary>
/// A packet handed to libvorbis, laid out like <c>ogg_packet</c> on 64-bit Windows where a C <c>long</c> is 32 bits.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct OggPacket
{
    public nint Data;
    public int Length;
    public int BeginningOfStream;
    public int EndOfStream;
    public long GranulePosition;
    public long PacketNumber;
}
