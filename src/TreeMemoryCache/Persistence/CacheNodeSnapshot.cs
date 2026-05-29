namespace TreeMemoryCache.Persistence;

/// <summary>
/// 节点快照，用于序列化和反序列化
/// </summary>
public sealed class CacheNodeSnapshot
{
    public required string Path { get; init; }
    public string? ParentPath { get; init; }
    public List<string> ChildPaths { get; init; } = new();
    public string? Tag { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public long Size { get; init; }
    public object? Value { get; init; }
}
