namespace TreeMemoryCache;

/// <summary>
/// 表示树形缓存中的节点元数据。
/// </summary>
/// <remarks>
/// <para>每个缓存值对应一个 <see cref="CacheNode"/>,维护在 <c>_nodes</c> 字典中。</para>
/// <para>中间路径节点(如 <c>A:B</c> 当 <c>A:B:C</c> 被设置时)也会创建节点,
/// 但 <see cref="Path"/> 上没有缓存值,这种节点不计入 <c>TotalCachedItems</c>。</para>
/// </remarks>
internal sealed class CacheNode
{
    /// <summary>
    /// 节点对应的缓存路径,如 <c>A:B:C</c>。
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// 父节点路径,根节点为 <c>null</c>。
    /// </summary>
    /// <remarks>
    /// 当父节点被级联删除但子节点保留时,该字段保持不变(指向已不存在的节点),
    /// 此时子节点成为"孤儿",可在 <see cref="Diagnostics.Validator.Validate"/> 中检测。
    /// </remarks>
    public string? ParentPath { get; set; }

    /// <summary>
    /// 直接子路径集合。
    /// </summary>
    public List<string> ChildPaths { get; set; } = new();

    /// <summary>
    /// 节点创建时间(UTC)。
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// 节点过期时间(UTC),与 <c>MemoryCacheEntryOptions.AbsoluteExpiration</c> 同步。
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// 节点缓存项的估算大小(字节),来源:显式 <c>options.Size</c>(优先)或 <see cref="ISizeEstimator"/>。
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// 节点关联的标签,可被 <see cref="ITreeMemoryCache.RemoveByTag"/> 或
    /// <see cref="ITreeMemoryCache.GetPathsByTag"/> 使用。
    /// </summary>
    public string? Tag { get; set; }

    /// <summary>
    /// 节点版本号,目前保留未使用,留给将来并发修改追踪。
    /// </summary>
    public int Version { get; set; }
}