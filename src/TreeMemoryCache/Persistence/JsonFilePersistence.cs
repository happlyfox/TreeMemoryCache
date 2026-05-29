using System.Collections.Concurrent;
using System.Text.Json;

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

    public Task<int> SaveAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public int Load(CancellationToken cancellationToken = default)
        => LoadAsync(cancellationToken).GetAwaiter().GetResult();

    public Task<int> LoadAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(0);

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
            NodeCount = 0,
            CreatedAt = fileInfo.CreationTimeUtc,
            SizeBytes = fileInfo.Length
        };
    }
}
