namespace Janitor.Wwise;

/// <summary>
/// An action of a Wwise event, such as playing or stopping a node.
/// </summary>
/// <param name="Id">The identifier of the action.</param>
/// <param name="Type">The type of the action, where the high byte is the kind of action and the low byte the scope it applies to.</param>
/// <param name="TargetId">The identifier of the node or bus the action is applied to.</param>
public sealed record WwiseAction(uint Id, ushort Type, uint TargetId)
{
    private const ushort PlayType = 0x0403;

    /// <summary>
    /// Gets a value indicating whether the action starts playing its target.
    /// </summary>
    public bool IsPlay => Type == PlayType;
}
