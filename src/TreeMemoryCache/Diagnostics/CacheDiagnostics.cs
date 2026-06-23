namespace TreeMemoryCache.Diagnostics;

/// <summary>
/// 综合诊断信息,通过 <see cref="ITreeMemoryCache.GetDiagnostics"/> 获取。
/// </summary>
/// <remarks>
/// 与 <see cref="CacheStatistics"/> 不同,本结构提供运行时结构状态(孤儿节点、
/// 标签分布、最深路径等)而非计数器,主要用于诊断与可视化。
/// </remarks>
public sealed class CacheDiagnostics
{
    /// <summary>
    /// 当前树索引节点总数(含中间节点)。
    /// </summary>
    public int TotalNodes { get; init; }

    /// <summary>
    /// 指向不存在父节点的"孤儿"节点数。
    /// </summary>
    /// <remarks>
    /// 这些节点通常是级联删除的副作用——父节点被删但子节点保留。
    /// 数量持续增长说明 RemoveTree 调用中存在数据完整性风险。
    /// </remarks>
    public int DeadParentLinks { get; init; }

    /// <summary>
    /// 所有缓存项估算内存占用(字节)。
    /// </summary>
    public long EstimatedMemoryBytes { get; init; }

    /// <summary>
    /// 标签 → 使用该标签的节点数,用于观察标签使用分布。
    /// </summary>
    public Dictionary<string, int> TagDistribution { get; init; } = new();

    /// <summary>
    /// 路径层级最深的若干路径(用于发现意外的深层结构)。
    /// </summary>
    public List<string> DeepestPaths { get; init; } = new();
}