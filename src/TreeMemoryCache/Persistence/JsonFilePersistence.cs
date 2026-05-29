using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace TreeMemoryCache.Persistence;

/// <summary>
/// JSON 文件持久化实现
/// </summary>
public sealed class JsonFilePersistence : ITreeCachePersistence
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ConcurrentDictionary<string, DateTime> _dirtyPaths = new();
    private readonly bool _enabled;

    public PersistenceStrategy Strategy { get; }
    public bool IsEnabled => _enabled;
    public DateTimeOffset? LastSavedAt { get; private set; }
    public int PendingChanges => _dirtyPaths.Count;

    public JsonFilePersistence(
        string filePath,
        PersistenceStrategy strategy = PersistenceStrategy.Synchronous)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        _filePath = filePath;
        Strategy = strategy;
        _enabled = true;

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public int Save(CancellationToken cancellationToken = default)
        => SaveAsync(cancellationToken).GetAwaiter().GetResult();

    public async Task<int> SaveAsync(CancellationToken cancellationToken = default)
    {
        if (!_enabled)
            return 0;

        if (_snapshots == null || _snapshots.Count == 0)
            return 0;

        // 序列化为 JSON
        var json = JsonSerializer.Serialize(_snapshots, _jsonOptions);

        // 原子写入
        var tempPath = _filePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, cancellationToken);

        if (File.Exists(_filePath))
            File.Delete(_filePath);
        File.Move(tempPath, _filePath);

        _dirtyPaths.Clear();
        LastSavedAt = DateTimeOffset.UtcNow;

        return _snapshots.Count;
    }

    public int Load(CancellationToken cancellationToken = default)
        => LoadAsync(cancellationToken).GetAwaiter().GetResult();

    public async Task<int> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!_enabled || !Exists())
            return 0;

        var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
        var snapshots = JsonSerializer.Deserialize<List<CacheNodeSnapshot>>(json, _jsonOptions);

        if (snapshots == null || snapshots.Count == 0)
            return 0;

        _snapshots = snapshots;
        return snapshots.Count;
    }

    public void MarkDirty(string path)
    {
        if (Strategy == PersistenceStrategy.Asynchronous)
        {
            _dirtyPaths[path] = DateTime.UtcNow;
        }
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (Strategy == PersistenceStrategy.Asynchronous && _dirtyPaths.Count > 0)
        {
            return SaveAsync(cancellationToken);
        }
        return Task.CompletedTask;
    }

    public bool Exists() => File.Exists(_filePath);

    public async ValueTask<StorageMetadata?> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        if (!Exists())
            return null;

        var fileInfo = new FileInfo(_filePath);
        return new StorageMetadata
        {
            NodeCount = _snapshots?.Count ?? 0,
            CreatedAt = fileInfo.CreationTimeUtc,
            SizeBytes = fileInfo.Length
        };
    }

    // 内部快照存储
    internal List<CacheNodeSnapshot>? _snapshots;

    /// <summary>
    /// 收集快照数据
    /// </summary>
    internal void CollectSnapshots(List<CacheNodeSnapshot> snapshots)
    {
        _snapshots = snapshots;
    }

    /// <summary>
    /// 提取快照数据
    /// </summary>
    internal List<CacheNodeSnapshot>? ExtractSnapshots()
    {
        return _snapshots;
    }
}
