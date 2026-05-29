namespace TreeMemoryCache.Persistence;

/// <summary>
/// 树形缓存持久化接口
/// </summary>
public interface ITreeCachePersistence
{
    PersistenceStrategy Strategy { get; }
    bool IsEnabled { get; }
    DateTimeOffset? LastSavedAt { get; }

    int Save(CancellationToken cancellationToken = default);
    int Load(CancellationToken cancellationToken = default);
    Task<int> SaveAsync(CancellationToken cancellationToken = default);
    Task<int> LoadAsync(CancellationToken cancellationToken = default);
    void MarkDirty(string path);
    Task FlushAsync(CancellationToken cancellationToken = default);
    bool Exists();
    ValueTask<StorageMetadata?> GetMetadataAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 存储元数据
/// </summary>
public sealed class StorageMetadata
{
    public int NodeCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public long SizeBytes { get; init; }
}
