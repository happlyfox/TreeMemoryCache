namespace TreeMemoryCache;

/// <summary>
/// 树形缓存运行时统计信息,通过 <see cref="ITreeMemoryCache.GetStatistics"/> 获取。
/// </summary>
/// <remarks>
/// 该结构是不可变快照,调用 <see cref="ITreeMemoryCache.GetStatistics"/> 当时立即
/// 拷贝内部计数,后续缓存的修改不会反映到已返回的实例上。
/// </remarks>
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
    /// 当前缓存估算总大小(字节)。
    /// </summary>
    /// <remarks>
    /// 来源于 <see cref="ISizeEstimator"/> 估算结果或调用方显式设置的
    /// <c>MemoryCacheEntryOptions.Size</c>(优先)。对于未配置估算器且未设置 Size
    /// 的条目,该项贡献 0 字节。
    /// </remarks>
    public long TotalCacheSize { get; init; }

    /// <summary>
    /// 通过 <see cref="ITreeMemoryCache.TryGetTree{T}(string, out T)"/> 或
    /// <see cref="ITreeMemoryCache.TryGetValue(object, out object?)"/> 命中缓存的累计次数。
    /// </summary>
    public long HitCount { get; init; }

    /// <summary>
    /// 通过 <see cref="ITreeMemoryCache.TryGetTree{T}(string, out T)"/> 或
    /// <see cref="ITreeMemoryCache.TryGetValue(object, out object?)"/> 未命中的累计次数。
    /// </summary>
    public long MissCount { get; init; }

    /// <summary>
    /// 累计执行的级联删除次数(每次 RemoveTree 算一次,与被删除的节点数无关)。
    /// </summary>
    public long CascadeDeleteCount { get; init; }

    /// <summary>
    /// 平均访问耗时(命中/未命中/级联删除耗时加权平均)。
    /// </summary>
    public TimeSpan AverageAccessTime { get; init; }

    /// <summary>
    /// 按根路径(第一段)分组的实际缓存条目数,用于诊断各命名空间的使用情况。
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
