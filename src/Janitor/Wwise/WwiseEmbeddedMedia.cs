namespace Janitor.Wwise;

/// <summary>
/// A single media file embedded in the data section of a Wwise sound bank.
/// </summary>
/// <param name="Id">The Wwise source identifier of the media, which is the name Wwise gives the stand-alone .wem file.</param>
/// <param name="Data">The raw .wem file bytes.</param>
public sealed record WwiseEmbeddedMedia(uint Id, byte[] Data);
