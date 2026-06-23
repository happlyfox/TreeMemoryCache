using System.Diagnostics;

namespace TreeMemoryCache.Debugging;

[DebuggerDisplay("{DebuggerDisplay,nq}", Name = "{DebuggerDisplay,nq}")]
internal sealed class TreeMemoryCacheDebugView
{
    private readonly TreeMemoryCache _cache;

    public TreeMemoryCacheDebugView(TreeMemoryCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public CacheNodeDebugView[] Nodes => _cache.GetNodesForDebug()
        .Select(kv => new CacheNodeDebugView(kv.Value))
        .ToArray();

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => $"TreeMemoryCache (节点: {_cache.GetStatistics().TotalNodeCount})";
}

[DebuggerDisplay("{Path} | {Tag ?? \"-\"}")]
internal sealed class CacheNodeDebugView
{
    private readonly CacheNode _node;

    public CacheNodeDebugView(CacheNode node) => _node = node;

    public string Path => _node.Path;
    public string? Tag => _node.Tag;
    public int ChildCount => _node.ChildPaths.Count;
}
