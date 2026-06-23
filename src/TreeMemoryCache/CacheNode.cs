namespace TreeMemoryCache;

/// <summary>
/// 表示树形缓存中的节点元数据。
/// </summary>
internal sealed class CacheNode
{
    public required string Path { get; init; }
    public string? ParentPath { get; set; }
    public List<string> ChildPaths { get; set; } = new();
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public long Size { get; set; }
    public string? Tag { get; set; }
    public int Version { get; set; }
}
