namespace TreeMemoryCache;

/// <summary>
/// 树形缓存运行时统计信息。
/// </summary>
public sealed class CacheStatistics
{
    /// <summary>
    /// 当前节点总数。
    /// </summary>
    /// <remarks>
    /// 实际语义同 <see cref="TotalCachedItems"/>——仅统计底层
    /// <c>IMemoryCache</c> 中能命中值的条目，不含中间路径节点。
    /// 保留为 <see cref="TotalCachedItems"/> 的向后兼容别名。
    /// 新代码请直接使用 <see cref="TotalCachedItems"/> 或 <see cref="TotalTrackedNodes"/>。
    /// </remarks>
    public long TotalNodeCount { get; init; }
    /// <summary>
    /// 当前缓存估算总大小。
    /// </summary>
    public long TotalCacheSize { get; init; }
    /// <summary>
    /// 命中次数。
    /// </summary>
    public long HitCount { get; init; }
    /// <summary>
    /// 未命中次数。
    /// </summary>
    public long MissCount { get; init; }
    /// <summary>
    /// 级联删除执行次数。
    /// </summary>
    public long CascadeDeleteCount { get; init; }
    /// <summary>
    /// 平均访问耗时。
    /// </summary>
    public TimeSpan AverageAccessTime { get; init; }
    /// <summary>
    /// 按根路径分组的节点数。
    /// </summary>
    public Dictionary<string, long> NodeCountByRoot { get; init; } = new();

    /// <summary>
    /// 实际存在缓存值的条目数（与 MemoryCache.Count 语义一致）。
    /// </summary>
    /// <remarks>
    /// 仅统计 <c>Microsoft.Extensions.Caching.Memory.IMemoryCache</c> 中能命中值的条目，不包含 EnsurePathExists
    /// 产生的"路径中间节点"。同一路径多次 SetTree 覆盖只计一次。
    /// </remarks>
    public long TotalCachedItems { get; init; }

    /// <summary>
    /// 树索引维护的节点总数，包含仅用于维护父子关系的中间节点。
    /// </summary>
    /// <remarks>
    /// 该值 >= <see cref="TotalCachedItems"/>。中间节点是路径层级遍历的副产品，
    /// 它们没有独立的缓存值，但承担路由职责。
    /// </remarks>
    public long TotalTrackedNodes { get; init; }
}
