using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace TreeMemoryCache.Persistence;

/// <summary>
/// 基于 JSON 文件的树形缓存持久化实现。
/// </summary>
/// <remarks>
/// <para>写入采用"写临时文件 + 原子重命名"模式,
/// 保证断电场景下不会留下半写文件,详见 <see cref="SaveAsync"/>。</para>
/// <para>对象值使用 <c>System.Text.Json</c> 默认序列化策略,
/// 复杂类型需要自行注册 <c>JsonConverter</c> 才能正确反序列化。</para>
/// </remarks>
public sealed class JsonFilePersistence : ITreeCachePersistence
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ConcurrentDictionary<string, DateTime> _dirtyPaths = new();
    private readonly bool _enabled;

    /// <inheritdoc />
    public PersistenceStrategy Strategy { get; }

    /// <inheritdoc />
    public bool IsEnabled => _enabled;

    /// <inheritdoc />
    public DateTimeOffset? LastSavedAt { get; private set; }

    /// <summary>
    /// 当前待写入的脏路径数(仅 <see cref="PersistenceStrategy.Asynchronous"/> 模式下有意义)。
    /// </summary>
    public int PendingChanges => _dirtyPaths.Count;

    /// <summary>
    /// 构造 JSON 文件持久化器。
    /// </summary>
    /// <param name="filePath">目标 JSON 文件路径,父目录不存在时自动创建。</param>
    /// <param name="strategy">持久化策略,默认 <see cref="PersistenceStrategy.Synchronous"/>。</param>
    /// <exception cref="ArgumentException">当 <paramref name="filePath"/> 为 null 或空字符串时抛出。</exception>
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

    /// <inheritdoc />
    public int Save(CancellationToken cancellationToken = default)
        => SaveAsync(cancellationToken).GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task<int> SaveAsync(CancellationToken cancellationToken = default)
    {
        if (!_enabled)
            return 0;

        if (_snapshots == null || _snapshots.Count == 0)
            return 0;

        // 序列化为 JSON
        var json = JsonSerializer.Serialize(_snapshots, _jsonOptions);

        // 原子写入:使用 File.Move(temp, target, overwrite: true) 一次原子替换。
        // 与之前"Delete + Move"序列不同,这里:
        // 1) 不再需要单独 Delete 步骤;
        // 2) Move(overwrite) 在 NTFS/Linux 上是原子的 rename,即使断电也不会留下半写状态;
        // 3) 不再有"Delete 失败但 Move 成功导致 target 是 tmp 文件"的隐患。
        var tempPath = _filePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, cancellationToken);
        File.Move(tempPath, _filePath, overwrite: true);

        _dirtyPaths.Clear();
        LastSavedAt = DateTimeOffset.UtcNow;

        return _snapshots.Count;
    }

    /// <inheritdoc />
    public int Load(CancellationToken cancellationToken = default)
        => LoadAsync(cancellationToken).GetAwaiter().GetResult();

    /// <inheritdoc />
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

    /// <inheritdoc />
    public void MarkDirty(string path)
    {
        if (Strategy == PersistenceStrategy.Asynchronous)
        {
            _dirtyPaths[path] = DateTime.UtcNow;
        }
    }

    /// <inheritdoc />
    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (Strategy == PersistenceStrategy.Asynchronous && _dirtyPaths.Count > 0)
        {
            return SaveAsync(cancellationToken);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public bool Exists() => File.Exists(_filePath);

    /// <inheritdoc />
    public ValueTask<StorageMetadata?> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Exists())
            return ValueTask.FromResult<StorageMetadata?>(null);

        var fileInfo = new FileInfo(_filePath);
        return ValueTask.FromResult<StorageMetadata?>(new StorageMetadata
        {
            NodeCount = _snapshots?.Count ?? 0,
            CreatedAt = fileInfo.CreationTimeUtc,
            SizeBytes = fileInfo.Length
        });
    }

    /// <summary>
    /// 内部快照存储(由 TreeMemoryCache.SaveAsync 通过回调注入)。
    /// </summary>
    internal List<CacheNodeSnapshot>? _snapshots;

    /// <summary>
    /// 收集快照数据(由 TreeMemoryCache.SaveAsync 调用)。
    /// </summary>
    internal void CollectSnapshots(List<CacheNodeSnapshot> snapshots)
    {
        _snapshots = snapshots;
    }

    /// <summary>
    /// 提取快照数据(由 TreeMemoryCache.LoadAsync 调用)。
    /// </summary>
    internal List<CacheNodeSnapshot>? ExtractSnapshots()
    {
        return _snapshots;
    }
}
