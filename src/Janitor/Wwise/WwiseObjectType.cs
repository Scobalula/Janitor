namespace Janitor.Wwise;

/// <summary>
/// The kinds of objects in the hierarchy (HIRC) section of a Wwise sound bank that are needed to trace media back to events.
/// </summary>
public enum WwiseObjectType : byte
{
    /// <summary>
    /// A sound that plays a single media source.
    /// </summary>
    Sound = 2,

    /// <summary>
    /// A random or sequence container.
    /// </summary>
    RandomSequenceContainer = 5,

    /// <summary>
    /// A switch container.
    /// </summary>
    SwitchContainer = 6,

    /// <summary>
    /// An actor-mixer, which groups sounds and containers.
    /// </summary>
    ActorMixer = 7,

    /// <summary>
    /// A blend or layer container.
    /// </summary>
    LayerContainer = 9,

    /// <summary>
    /// A music segment, which groups music tracks.
    /// </summary>
    MusicSegment = 10,

    /// <summary>
    /// A music track that plays one or more media sources.
    /// </summary>
    MusicTrack = 11,

    /// <summary>
    /// A music switch container.
    /// </summary>
    MusicSwitchContainer = 12,

    /// <summary>
    /// A music random or sequence container.
    /// </summary>
    MusicRandomSequenceContainer = 13,
}
