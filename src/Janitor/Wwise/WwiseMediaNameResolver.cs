namespace Janitor.Wwise;

/// <summary>
/// Resolves the identifiers of Wwise media (.wem) files to the names of the sound banks that play them.
/// </summary>
/// <remarks>
/// Media is only ever referenced by identifier, but every event of the game has its own sound bank named after the event.
/// Media is named after the bank whose events play it, found by following the play actions of each event down the hierarchy
/// to the sounds and music tracks that reference the media. Media that is shared between events is named after the bank that
/// plays the fewest media, as that is the event it is most specific to. Media that no play action reaches falls back to the
/// banks that reference it directly.
/// </remarks>
public sealed class WwiseMediaNameResolver
{
    private readonly Dictionary<uint, WwiseNode> _nodes = [];
    private readonly Dictionary<uint, WwiseAction> _actions = [];
    private readonly Dictionary<string, List<WwiseEvent>> _eventsByBank = [];
    private readonly Dictionary<string, HashSet<uint>> _referencedMediaByBank = [];

    /// <summary>
    /// The name of the name table the resolved names are stored in.
    /// </summary>
    public const string TableName = "WwiseMediaTable";

    /// <summary>
    /// Adds a sound bank to the hierarchy that media is resolved from. Banks may reference objects in other banks, so all banks should be added before resolving.
    /// </summary>
    /// <param name="bankName">The name of the bank, which is the name of the event it belongs to.</param>
    /// <param name="bank">The parsed bank.</param>
    public void AddBank(string bankName, WwiseBank bank)
    {
        foreach (var node in bank.Nodes)
            _nodes[node.Id] = node;

        foreach (var action in bank.Actions)
            _actions[action.Id] = action;

        _eventsByBank[bankName] = bank.Events;
        _referencedMediaByBank[bankName] = [.. bank.Nodes.SelectMany(x => x.SourceIds)];
    }

    /// <summary>
    /// Resolves the names of all media referenced by the added banks.
    /// </summary>
    /// <returns>The name of each media file, keyed by its identifier.</returns>
    public Dictionary<uint, string> Resolve()
    {
        var childrenByParent = _nodes.Values.Where(x => x.ParentId != 0).ToLookup(x => x.ParentId);
        var playedMediaByBank = new Dictionary<string, HashSet<uint>>();

        foreach (var (bankName, events) in _eventsByBank)
        {
            var playedMedia = new HashSet<uint>();

            foreach (var playAction in events.SelectMany(x => x.ActionIds).Select(x => _actions.GetValueOrDefault(x)).Where(x => x is { IsPlay: true }))
                CollectMedia(playAction!.TargetId, childrenByParent, playedMedia, []);

            playedMediaByBank[bankName] = playedMedia;
        }

        var names = ResolveMostSpecificBank(_referencedMediaByBank);

        foreach (var (mediaId, name) in ResolveMostSpecificBank(playedMediaByBank))
            names[mediaId] = name;

        return names;
    }

    private void CollectMedia(uint nodeId, ILookup<uint, WwiseNode> childrenByParent, HashSet<uint> media, HashSet<uint> visited)
    {
        if (!visited.Add(nodeId))
            return;

        if (_nodes.TryGetValue(nodeId, out var node))
            media.UnionWith(node.SourceIds);

        foreach (var child in childrenByParent[nodeId])
            CollectMedia(child.Id, childrenByParent, media, visited);
    }

    private static Dictionary<uint, string> ResolveMostSpecificBank(Dictionary<string, HashSet<uint>> mediaByBank)
    {
        var names = new Dictionary<uint, string>();
        var mediaCounts = new Dictionary<uint, int>();

        foreach (var (bankName, media) in mediaByBank.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            foreach (var mediaId in media)
            {
                if (names.ContainsKey(mediaId) && mediaCounts[mediaId] <= media.Count)
                    continue;

                names[mediaId] = bankName;
                mediaCounts[mediaId] = media.Count;
            }
        }

        return names;
    }
}
