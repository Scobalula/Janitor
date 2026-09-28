namespace Janitor.Wwise;

/// <summary>
/// An event of a Wwise sound bank, which is triggered by the game and runs a list of actions.
/// </summary>
/// <param name="Id">The identifier of the event, which is the hash of its name.</param>
/// <param name="ActionIds">The identifiers of the actions the event runs.</param>
public sealed record WwiseEvent(uint Id, uint[] ActionIds);
