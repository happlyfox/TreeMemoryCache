namespace TreeMemoryCache.Persistence;

/// <summary>
/// 节点快照,用于序列化和反序列化单个 <c>CacheNode</c> 到 JSON。
/// </summary>
/// <remarks>
/// <para>由 <c>JsonFilePersistence</c> 在 SaveAsync 时生成,LoadAsync 时解析回原节点。</para>
/// <para>该结构仅用于持久化层,不应在业务代码中直接使用。</para>
/// </remarks>
public sealed class CacheNodeSnapshot
{
    /// <summary>
    /// 节点路径。
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// 父节点路径,根节点为 <c>null</c>。
    /// </summary>
    public string? ParentPath { get; init; }

    /// <summary>
    /// 直接子路径集合。
    /// </summary>
    public List<string> ChildPaths { get; init; } = new();

    /// <summary>
    /// 节点标签。
    /// </summary>
    public string? Tag { get; init; }

    /// <summary>
    /// 节点创建时间(UTC)。
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// 节点过期时间(UTC)。
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// 节点估算大小(字节)。
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// 实际缓存值。Load 时会还原到 <c>IMemoryCache</c>。
    /// </summary>
    public object? Value { get; init; }
}