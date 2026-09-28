namespace Janitor.Wwise;

/// <summary>
/// A node in the audio hierarchy of a Wwise sound bank, such as a sound, a container or a music segment.
/// </summary>
/// <param name="Id">The identifier of the node.</param>
/// <param name="Type">The kind of node.</param>
/// <param name="ParentId">The identifier of the parent node, or zero when the node is at the top of the hierarchy.</param>
/// <param name="SourceIds">The identifiers of the media sources the node plays, which are the names of the .wem files. Only sounds and music tracks have any.</param>
public sealed record WwiseNode(uint Id, WwiseObjectType Type, uint ParentId, uint[] SourceIds);
