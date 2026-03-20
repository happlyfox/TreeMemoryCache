namespace TreeMemoryCache;

/// <summary>
/// 树形缓存运行时统计信息。
/// </summary>
public sealed class CacheStatistics
{
    /// <summary>
    /// 当前节点总数。
    /// </summary>
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
}
