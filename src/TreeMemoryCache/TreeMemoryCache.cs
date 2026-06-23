using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TreeMemoryCache.Diagnostics;
using TreeMemoryCache.Logging;
using TreeMemoryCache.Persistence;

namespace TreeMemoryCache;

/// <summary>
/// 基于 MemoryCache 的树形缓存实现，支持路径层级维护与级联删除。
/// </summary>
public sealed class TreeMemoryCache : ITreeMemoryCache
{
    private readonly MemoryCache _innerCache;
    private readonly ConcurrentDictionary<string, CacheNode> _nodes;
    private readonly ConcurrentDictionary<string, HashSet<string>> _tagIndex;
    // 第一段索引:第一段 -> 该段下所有 path。
    // 用于加速 GetPathsByPattern("A:*") / "A:B:*" 等通配符查询,
    // 避免对 _nodes 做全表扫描。
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, CacheNode>> _segmentIndex;
    private readonly ReaderWriterLockSlim _structureLock;
    private readonly ILogger<TreeMemoryCache>? _logger;
    private readonly CacheStatisticsCollector _statistics;
    private readonly ITreeCachePersistence? _persistence;
    private readonly ISizeEstimator _sizeEstimator;
    private int _disposed;

    /// <summary>
    /// 获取持久化器实例。如果构造时未传入持久化器则返回 <c>null</c>,此时 SaveAsync/LoadAsync 为 no-op。
    /// </summary>
    public ITreeCachePersistence? Persistence => _persistence;

    /// <summary>
    /// 初始化 TreeMemoryCache 实例。
    /// </summary>
    /// <param name="persistence">持久化器实例，默认为 null（无持久化）。</param>
    /// <param name="options">内存缓存选项。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="sizeEstimator">自定义大小估算器,默认为 <see cref="DefaultSizeEstimator"/>。</param>
    public TreeMemoryCache(
        ITreeCachePersistence? persistence = null,
        MemoryCacheOptions? options = null,
        ILogger<TreeMemoryCache>? logger = null,
        ISizeEstimator? sizeEstimator = null)
    {
        _innerCache = new MemoryCache(options ?? new MemoryCacheOptions());
        _nodes = new ConcurrentDictionary<string, CacheNode>(StringComparer.Ordinal);
        _tagIndex = new ConcurrentDictionary<string, HashSet<string>>(StringComparer.Ordinal);
        _segmentIndex = new ConcurrentDictionary<string, ConcurrentDictionary<string, CacheNode>>(StringComparer.Ordinal);
        // 使用 NoRecursion 策略:禁止同一线程递归获取锁。
// 理由:在持锁回调(OnCacheEntryEvicted)中若试图重入,会立即抛
// LockRecursionException,把"持锁调用户代码"这类隐性死锁暴露为编译/运行期错误。
// 当前所有持锁回调路径(RemoveSingleNode)都不再调 _innerCache,故 NoRecursion 安全。
_structureLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        _logger = logger;
        _statistics = new CacheStatisticsCollector();
        _persistence = persistence;
        _sizeEstimator = sizeEstimator ?? DefaultSizeEstimator.Instance;
    }

    private void EnsureNotDisposed()
    {
        if (_disposed != 0)
            throw new ObjectDisposedException(nameof(TreeMemoryCache));
    }

    /// <inheritdoc />
    public ICacheEntry CreateEntry(object key)
    {
        EnsureNotDisposed();
        return _innerCache.CreateEntry(key);
    }

    /// <inheritdoc />
    public void Remove(object key)
    {
        EnsureNotDisposed();
        if (key is string path)
        {
            RemoveInternal(path);
            return;
        }
        _innerCache.Remove(key);
    }

    /// <inheritdoc />
    public bool TryGetValue(object key, out object? value)
    {
        EnsureNotDisposed();
        var stopwatch = Stopwatch.StartNew();
        var result = _innerCache.TryGetValue(key, out value);
        stopwatch.Stop();

        if (key is string path)
        {
            if (result)
            {
                _statistics.RecordHit(stopwatch.Elapsed);
            }
            else
            {
                _statistics.RecordMiss();
            }
        }

        return result;
    }

    /// <inheritdoc />
    public bool TryGetTree<T>(string path, out T? value)
    {
        EnsureNotDisposed();
        ArgumentNullException.ThrowIfNull(path);
        var normalizedPath = NormalizePath(path);
        if (normalizedPath.Length == 0 && !_innerCache.TryGetValue(string.Empty, out _))
        {
            throw new ArgumentException("The value cannot be an empty string.", nameof(path));
        }

        var stopwatch = Stopwatch.StartNew();
        var result = _innerCache.TryGetValue(normalizedPath, out var obj);
        stopwatch.Stop();

        if (result && obj is T typedValue)
        {
            _statistics.RecordHit(stopwatch.Elapsed);
            value = typedValue;
            return true;
        }

        _statistics.RecordMiss();
        value = default;
        return false;
    }

    /// <inheritdoc />
    public ICacheEntry SetTree<T>(string path, T value, MemoryCacheEntryOptions? options = null)
    {
        return SetTree(path, value, tag: null, options);
    }

    /// <inheritdoc />
    public ICacheEntry SetTree<T>(string path, T value, string? tag, MemoryCacheEntryOptions? options = null)
    {
        EnsureNotDisposed();
        ArgumentException.ThrowIfNullOrEmpty(path);

        options ??= new MemoryCacheEntryOptions();

        var normalizedPath = NormalizePath(path);
        var segments = ParsePathSegments(normalizedPath);

        _structureLock.EnterWriteLock();
        try
        {
            return SetTreeUnderLock(normalizedPath, segments, value, tag, options);
        }
        finally
        {
            _structureLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// 写锁内执行的 SetTree 核心逻辑。调用方必须已持有写锁。
    /// </summary>
    /// <remarks>
    /// 抽离出来供 <see cref="ExecuteBatch"/> 直接调用,避免"持锁调 SetTree"的递归死锁。
    /// </remarks>
    private ICacheEntry SetTreeUnderLock<T>(
        string normalizedPath,
        ReadOnlySpan<string> segments,
        T value,
        string? tag,
        MemoryCacheEntryOptions options)
    {
        EnsurePathExists(normalizedPath, segments, tag);

        var entry = _innerCache.CreateEntry(normalizedPath);
        entry.Value = value;

        if (options.AbsoluteExpiration.HasValue)
            entry.AbsoluteExpiration = options.AbsoluteExpiration.Value;
        if (options.AbsoluteExpirationRelativeToNow.HasValue)
            entry.AbsoluteExpirationRelativeToNow = options.AbsoluteExpirationRelativeToNow.Value;
        if (options.SlidingExpiration.HasValue)
            entry.SlidingExpiration = options.SlidingExpiration.Value;
        if (options.Size.HasValue)
            entry.Size = options.Size.Value;
        if (options.Priority != CacheItemPriority.Normal)
            entry.Priority = options.Priority;

        entry.RegisterPostEvictionCallback(OnCacheEntryEvicted, normalizedPath);

        if (_nodes.TryGetValue(normalizedPath, out var node))
        {
            // 树节点 Size 优先尊重显式 options.Size(契约:调用方负责),
            // 未显式设置时回退到 ISizeEstimator 估算。
            node.Size = options.Size ?? EstimateSize(value);
            if (options.AbsoluteExpiration.HasValue)
                node.ExpiresAt = options.AbsoluteExpiration.Value;

            // 如果标签发生变化，先从旧索引移除
            if (node.Tag != tag && node.Tag is not null)
            {
                if (_tagIndex.TryGetValue(node.Tag, out var oldPaths))
                {
                    oldPaths.Remove(normalizedPath);
                    if (oldPaths.Count == 0)
                    {
                        _tagIndex.TryRemove(node.Tag, out _);
                    }
                }
            }

            // 无论什么情况，只要新标签不为空，添加到索引
            if (tag is not null)
            {
                var taggedPaths = _tagIndex.GetOrAdd(tag, _ => new HashSet<string>(StringComparer.Ordinal));
                taggedPaths.Add(normalizedPath);
            }

            // 总是更新节点标签
            if (node.Tag != tag)
            {
                Console.Error.WriteLine($"[DEBUG] node-exists path={normalizedPath}, oldTag={node.Tag ?? "<null>"}, newTag={tag ?? "<null>"}, _tagIndex has tag1={_tagIndex.ContainsKey("tag1")}, has tag2={_tagIndex.ContainsKey("tag2")}");
                node.Tag = tag;
            }
        }
        else
        {
            // 节点不存在，但这里不应该走到，因为 EnsurePathExists 已经创建了
            // 防御性处理：确保节点存在
            var nodeBuilder = new CacheNode
            {
                Path = normalizedPath,
                CreatedAt = DateTimeOffset.UtcNow,
                Tag = tag
            };
            _nodes.TryAdd(normalizedPath, nodeBuilder);
            if (tag is not null)
            {
                var taggedPaths = _tagIndex.GetOrAdd(tag, _ => new HashSet<string>(StringComparer.Ordinal));
                taggedPaths.Add(normalizedPath);
            }
            node = nodeBuilder;
        }

        // 同步段索引:把 normalizedPath 加入第一段对应的桶
        var firstSegment = normalizedPath.Split(':', 2)[0];
        var bucket = _segmentIndex.GetOrAdd(firstSegment,
            _ => new ConcurrentDictionary<string, CacheNode>(StringComparer.Ordinal));
        bucket[normalizedPath] = node!;

        _statistics.RecordSet();
        return entry;
    }

    /// <inheritdoc />
    public void RemoveTree(string path, TreeRemoveOptions? options = null)
    {
        EnsureNotDisposed();
        ArgumentException.ThrowIfNullOrEmpty(path);
        options ??= new TreeRemoveOptions();

        var normalizedPath = NormalizePath(path);
        var stopwatch = Stopwatch.StartNew();

        _structureLock.EnterWriteLock();
        try
        {
            var pathsToRemove = CollectDescendants(normalizedPath);
            if (options.IncludeSelf)
            {
                pathsToRemove.Insert(0, normalizedPath);
            }
            else
            {
                pathsToRemove = pathsToRemove.Where(p => p != normalizedPath).ToList();
            }

            var count = 0;
            foreach (var nodePath in pathsToRemove)
            {
                RemoveInternal(nodePath);
                count++;

                options.OnProgress?.Invoke(count);
            }

            stopwatch.Stop();
            _statistics.RecordCascadeDelete(count, stopwatch.Elapsed);
            _logger?.LogInformation(
                "Cascade delete completed: {Path}, {Count} nodes removed in {Duration}ms",
                normalizedPath, count, stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            _structureLock.ExitWriteLock();
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> RemoveTreeAsync(string path, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        ArgumentException.ThrowIfNullOrEmpty(path);

        var normalizedPath = NormalizePath(path);

        // 在单个写锁内完成所有收集和删除操作，避免竞态条件
        _structureLock.EnterWriteLock();
        List<string> pathsToRemove;
        try
        {
            pathsToRemove = CollectDescendants(normalizedPath);
            pathsToRemove.Insert(0, normalizedPath);

            foreach (var nodePath in pathsToRemove)
            {
                RemoveInternal(nodePath);
            }
        }
        finally
        {
            _structureLock.ExitWriteLock();
        }

        // 释放锁后再 yield return，避免在锁内 await
        foreach (var nodePath in pathsToRemove)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return nodePath;
        }
    }

    /// <inheritdoc />
    public IEnumerable<string> GetChildPaths(string path)
    {
        EnsureNotDisposed();
        ArgumentException.ThrowIfNullOrEmpty(path);

        var normalizedPath = NormalizePath(path);

        _structureLock.EnterReadLock();
        try
        {
            if (_nodes.TryGetValue(normalizedPath, out var node))
            {
                return node.ChildPaths.ToList();
            }
            return Enumerable.Empty<string>();
        }
        finally
        {
            _structureLock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public IEnumerable<string> GetDescendantPaths(string path)
    {
        EnsureNotDisposed();
        ArgumentException.ThrowIfNullOrEmpty(path);

        var normalizedPath = NormalizePath(path);

        _structureLock.EnterReadLock();
        try
        {
            return CollectDescendants(normalizedPath);
        }
        finally
        {
            _structureLock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public IEnumerable<string> GetPathsByPattern(string pattern)
    {
        EnsureNotDisposed();
        ArgumentException.ThrowIfNullOrEmpty(pattern);

        _structureLock.EnterReadLock();
        try
        {
            // 第一段索引优化:模式形如 "Segment:*"、"Segment:Sub:*" 等,
            // 直接从段索引的对应桶里取,不必遍历 _nodes 全表。
            if (pattern.Contains('*'))
            {
                var firstSegment = GetFirstSegmentFromPattern(pattern);
                if (firstSegment is not null && _segmentIndex.TryGetValue(firstSegment, out var bucket))
                {
                    var patternSegments = pattern.Split(':');
                    return bucket.Keys
                        .Where(path => MatchesPattern(path.Split(':'), patternSegments))
                        .ToList();
                }
            }

            return pattern switch
            {
                "*" => _nodes.Keys.ToList(),
                "*:*" => _nodes.Keys.Where(k => k.Contains(':')).ToList(),
                _ when pattern.Contains('*') => MatchPattern(pattern).ToList(),
                _ => _nodes.Keys.Where(k => k == pattern).ToList()
            };
        }
        finally
        {
            _structureLock.ExitReadLock();
        }
    }

    /// <summary>
    /// 从通配符模式中提取第一段(如果第一段不是通配符)。
    /// 例如 "A:*" 返回 "A","A:B:*" 返回 "A","*:*" 返回 null,"*" 返回 null。
    /// </summary>
    private static string? GetFirstSegmentFromPattern(string pattern)
    {
        var firstSeparator = pattern.IndexOf(':');
        if (firstSeparator <= 0)
            return null;
        var firstSegment = pattern[..firstSeparator];
        return firstSegment == "*" ? null : firstSegment;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetPathsByTag(string tag)
    {
        EnsureNotDisposed();
        ArgumentException.ThrowIfNullOrEmpty(tag);

        if (_tagIndex.TryGetValue(tag, out var paths))
        {
            return paths.ToList();
        }
        return Enumerable.Empty<string>();
    }

    /// <inheritdoc />
    public void RemoveByTag(string tag)
    {
        EnsureNotDisposed();
        ArgumentException.ThrowIfNullOrEmpty(tag);

        // 在单个写锁内完成"快照 + 逐条删除 + 清理标签索引",
        // 避免其他线程在快照后修改 tag 或路径导致的脏数据。
        _structureLock.EnterWriteLock();
        try
        {
            if (!_tagIndex.TryGetValue(tag, out var paths))
                return;

            // 复制为本地 list,避免在迭代 HashSet 时其他回调修改它
            var snapshot = paths.ToList();
            foreach (var path in snapshot)
            {
                RemoveInternal(path);
            }

            _tagIndex.TryRemove(tag, out _);
        }
        finally
        {
            _structureLock.ExitWriteLock();
        }
    }

    /// <inheritdoc />
    public CacheStatistics GetStatistics()
    {
        EnsureNotDisposed();
        _structureLock.EnterReadLock();
        try
        {
            var nodeCountByRoot = new Dictionary<string, long>();
            long trackedNodeCount = 0;
            long cachedItemCount = 0;
            long totalCacheSize = 0;

            foreach (var (path, node) in _nodes)
            {
                trackedNodeCount++;

                // 只有 _innerCache 里真正存在的才算"cached item"
                if (!_innerCache.TryGetValue(path, out _))
                {
                    continue;
                }

                cachedItemCount++;
                totalCacheSize += node.Size;

                var root = GetRootPath(path);
                nodeCountByRoot.TryGetValue(root, out var count);
                nodeCountByRoot[root] = count + 1;
            }

            return new CacheStatistics
            {
                // 向后兼容:TotalNodeCount 仍表示"实际缓存条目数"(等同 TotalCachedItems)
                TotalNodeCount = cachedItemCount,
                TotalCachedItems = cachedItemCount,
                TotalTrackedNodes = trackedNodeCount,
                TotalCacheSize = totalCacheSize,
                HitCount = _statistics.HitCount,
                MissCount = _statistics.MissCount,
                CascadeDeleteCount = _statistics.CascadeDeleteCount,
                AverageAccessTime = _statistics.AverageAccessTime,
                NodeCountByRoot = nodeCountByRoot
            };
        }
        finally
        {
            _structureLock.ExitReadLock();
        }
    }

    internal IReadOnlyDictionary<string, CacheNode> GetNodesForDebug()
    {
        EnsureNotDisposed();
        return _nodes;
    }

    /// <summary>
    /// 获取所有根节点（没有父节点的节点）。
    /// </summary>
    internal IEnumerable<KeyValuePair<string, CacheNode>> GetRootNodes()
    {
        EnsureNotDisposed();
        _structureLock.EnterReadLock();
        try
        {
            return _nodes.Where(kv => kv.Value.ParentPath is null).ToList();
        }
        finally
        {
            _structureLock.ExitReadLock();
        }
    }

    /// <summary>
    /// 获取所有缓存节点的快照，用于持久化。
    /// </summary>
    internal List<CacheNodeSnapshot> GetAllSnapshots()
    {
        var snapshots = new List<CacheNodeSnapshot>();

        _structureLock.EnterReadLock();
        try
        {
            foreach (var (path, node) in _nodes)
            {
                _innerCache.TryGetValue(path, out var value);

                snapshots.Add(new CacheNodeSnapshot
                {
                    Path = node.Path,
                    ParentPath = node.ParentPath,
                    ChildPaths = node.ChildPaths.ToList(),
                    Tag = node.Tag,
                    CreatedAt = node.CreatedAt,
                    ExpiresAt = node.ExpiresAt,
                    Size = node.Size,
                    Value = value
                });
            }
        }
        finally
        {
            _structureLock.ExitReadLock();
        }

        return snapshots;
    }

    /// <summary>
    /// 从快照列表恢复缓存节点，用于加载持久化数据。
    /// </summary>
    internal void RestoreFromSnapshots(IEnumerable<CacheNodeSnapshot> snapshots)
    {
        _structureLock.EnterWriteLock();
        try
        {
            foreach (var snapshot in snapshots)
            {
                // 跳过已过期的快照
                if (snapshot.ExpiresAt.HasValue && snapshot.ExpiresAt.Value <= DateTimeOffset.UtcNow)
                    continue;

                // 重建 CacheNode
                var node = new CacheNode
                {
                    Path = snapshot.Path,
                    ParentPath = snapshot.ParentPath,
                    ChildPaths = new List<string>(snapshot.ChildPaths),
                    CreatedAt = snapshot.CreatedAt,
                    ExpiresAt = snapshot.ExpiresAt,
                    Size = snapshot.Size,
                    Tag = snapshot.Tag
                };

                _nodes[snapshot.Path] = node;

                // 父节点的子路径由 snapshot.ChildPaths 重建时已经处理,
                // 这里不再重复 Add(snapshot.Path),否则 List 会出现重复。
                // (HashSet 时代靠自动去重掩盖了这个问题。)

                // 重建缓存值
                if (snapshot.Value != null)
                {
                    var entry = _innerCache.CreateEntry(snapshot.Path);
                    entry.Value = snapshot.Value;
                    if (snapshot.ExpiresAt.HasValue)
                        entry.AbsoluteExpiration = snapshot.ExpiresAt.Value;
                    entry.Dispose();
                }

                // 重建标签索引
                if (!string.IsNullOrEmpty(snapshot.Tag))
                {
                    var taggedPaths = _tagIndex.GetOrAdd(snapshot.Tag, _ => new HashSet<string>(StringComparer.Ordinal));
                    taggedPaths.Add(snapshot.Path);
                }
            }
        }
        finally
        {
            _structureLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// 将当前所有缓存项序列化到持久化存储。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已保存的节点数量;未配置持久化或未启用时返回 0。</returns>
    /// <remarks>
    /// 当前仅对 <see cref="Persistence.JsonFilePersistence"/> 提供内置实现,
    /// 其他持久化器需要在 LoadAsync 之前通过回调方式注入快照。
    /// </remarks>
    public async Task<int> SaveAsync(CancellationToken cancellationToken = default)
    {
        if (_persistence == null || !_persistence.IsEnabled)
            return 0;

        // 收集所有快照
        var snapshots = GetAllSnapshots();

        // 通过事件让持久化器获取快照
        if (_persistence is JsonFilePersistence jfp)
        {
            jfp.CollectSnapshots(snapshots);
        }

        return await _persistence.SaveAsync(cancellationToken);
    }

    /// <summary>
    /// 从持久化存储加载缓存数据并恢复到当前实例。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已加载的节点数量;未配置持久化或存储文件不存在时返回 0。</returns>
    /// <remarks>
    /// 加载会**追加**到现有缓存,不会清空。如需重置请新建实例。
    /// </remarks>
    public async Task<int> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_persistence == null || !_persistence.IsEnabled)
            return 0;

        if (!_persistence.Exists())
            return 0;

        // 通过事件让持久化器恢复快照
        if (_persistence is JsonFilePersistence jfp)
        {
            var snapshots = jfp.ExtractSnapshots();
            if (snapshots != null)
            {
                RestoreFromSnapshots(snapshots);
                return snapshots.Count;
            }
        }

        return await _persistence.LoadAsync(cancellationToken);
    }

    /// <summary>
    /// 获取综合诊断信息,包括孤儿节点、标签分布、最深路径等。
    /// </summary>
    /// <returns>当前的 <see cref="Diagnostics.CacheDiagnostics"/> 快照。</returns>
    /// <exception cref="ObjectDisposedException">当缓存已 Dispose 后调用时抛出。</exception>
    public CacheDiagnostics GetDiagnostics()
    {
        EnsureNotDisposed();
        _structureLock.EnterReadLock();
        try
        {
            var deadParentLinks = 0;
            var tagDistribution = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var (path, node) in _nodes)
            {
                if (node.ParentPath is not null && !_nodes.ContainsKey(node.ParentPath))
                {
                    deadParentLinks++;
                }

                if (node.Tag is not null)
                {
                    tagDistribution.TryGetValue(node.Tag, out var count);
                    tagDistribution[node.Tag] = count + 1;
                }
            }

            var deepestPaths = _nodes.Keys
                .OrderByDescending(p => p.Split(':').Length)
                .Take(5)
                .ToList();

            return new CacheDiagnostics
            {
                TotalNodes = _nodes.Count,
                DeadParentLinks = deadParentLinks,
                EstimatedMemoryBytes = _nodes.Values.Sum(n => n.Size),
                TagDistribution = tagDistribution,
                DeepestPaths = deepestPaths
            };
        }
        finally
        {
            _structureLock.ExitReadLock();
        }
    }

    /// <summary>
    /// 验证缓存树结构的一致性,检查死父引用、孤立标签等。
    /// </summary>
    /// <returns>包含错误与警告的 <see cref="Diagnostics.ValidationResult"/>。<c>IsValid=true</c> 表示无错误。</returns>
    /// <exception cref="ObjectDisposedException">当缓存已 Dispose 后调用时抛出。</exception>
    public ValidationResult Validate()
    {
        EnsureNotDisposed();
        return Validator.Validate(_nodes, _tagIndex);
    }

    private readonly ConcurrentQueue<OperationRecord> _operationHistory = new();
    private const int MaxTrackedOperations = 1000;

    /// <summary>
    /// 获取最近的操作历史记录(最多 1000 条,按时间倒序)。
    /// </summary>
    /// <returns>最近的 <see cref="Diagnostics.OperationRecord"/> 列表。</returns>
    /// <remarks>
    /// 主要用于诊断与调试,生产代码不应依赖此 API。
    /// </remarks>
    public IReadOnlyList<OperationRecord> GetOperationHistory()
    {
        return _operationHistory.ToArray().Reverse().Take(100).Reverse().ToList();
    }

    private void TrackOperation(OperationType type, string path, string? tag = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var record = new OperationRecord
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = type,
            Path = path,
            Tag = tag,
            CallerMemberName = memberName,
            CallerFilePath = filePath,
            CallerLineNumber = lineNumber
        };

        _operationHistory.Enqueue(record);
        while (_operationHistory.Count > MaxTrackedOperations)
        {
            _operationHistory.TryDequeue(out _);
        }
    }

    /// <inheritdoc />
    public TreeCacheBatch CreateBatch()
    {
        EnsureNotDisposed();
        return new TreeCacheBatch(this);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // 使用原子操作确保只有一个线程执行清理
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        // 先释放内部资源（可能触发驱逐回调），此时还未持有锁
        _innerCache.Dispose();
        _nodes.Clear();
        _tagIndex.Clear();

        // 确保锁正确释放和销毁
        _structureLock?.Dispose();
    }

    /// <summary>
    /// 确保完整路径上的每一级节点都存在，并维护父子关系。
    /// 标签设置在目标路径节点上（最后一段）。
    /// </summary>
    private void EnsurePathExists(string path, ReadOnlySpan<string> segments, string? tag)
    {
        for (var i = 0; i < segments.Length; i++)
        {
            var currentPath = string.Join(':', segments.Slice(0, i + 1).ToArray());
            var parentPath = i > 0 ? string.Join(':', segments.Slice(0, i).ToArray()) : null;
            var isTargetNode = i == segments.Length - 1;

            if (!_nodes.ContainsKey(currentPath))
            {
                var node = new CacheNode
                {
                    Path = currentPath,
                    ParentPath = parentPath,
                    CreatedAt = DateTimeOffset.UtcNow,
                    // 只有目标节点（用户调用 SetTree 的路径）才设置标签，中间节点始终为 null
                    Tag = isTargetNode ? tag : null
                };

                _nodes.TryAdd(currentPath, node);

                if (parentPath is not null && _nodes.TryGetValue(parentPath, out var parentNode))
                {
                    parentNode.ChildPaths.Add(currentPath);
                }
            }
        }
    }

    /// <summary>
    /// 删除单个节点并清理其父子引用与标签索引。
    /// 注意：本方法只删除指定节点本身，调用方需自行负责级联删除。
    /// </summary>
    private void RemoveSingleNode(string path)
    {
        if (_nodes.TryRemove(path, out var node))
        {
            // 从父节点的子节点列表中移除
            if (node.ParentPath is not null && _nodes.TryGetValue(node.ParentPath, out var parentNode))
            {
                parentNode.ChildPaths.Remove(path);
            }

            // 清理标签索引
            if (node.Tag is not null && _tagIndex.TryGetValue(node.Tag, out var taggedPaths))
            {
                taggedPaths.Remove(path);
                // 如果标签集合为空，清理整个标签索引条目
                if (taggedPaths.Count == 0)
                {
                    _tagIndex.TryRemove(node.Tag, out _);
                }
            }

            // 同步段索引:从第一段对应的桶中移除
            var firstSegment = path.Split(':', 2)[0];
            if (_segmentIndex.TryGetValue(firstSegment, out var bucket))
            {
                bucket.TryRemove(path, out _);
                // 如果桶为空,清理整个段索引条目(避免 _segmentIndex 累积空桶)
                if (bucket.IsEmpty)
                {
                    _segmentIndex.TryRemove(firstSegment, out _);
                }
            }
        }
    }

    /// <summary>
    /// 统一的内部删除入口：同时清理树索引和 _innerCache。
    /// 所有 TreeMemoryCache 内部触发的删除（包括 Remove/RemoveTree/ExecuteBatch/OnCacheEntryEvicted）
    /// 都应走此方法，避免索引不同步。
    /// </summary>
    private void RemoveInternal(string path)
    {
        RemoveSingleNode(path);
        _innerCache.Remove(path);
    }

    /// <summary>
    /// 广度优先收集指定路径下的全部后代路径(不含 path 自身)。
    /// </summary>
    /// <remarks>
    /// 使用 HashSet 去重防御罕见回环场景(目前 RemoveSingleNode 会断开
    /// 子节点的 ParentPath 但不主动重连,理论上不存在回环;此处保留防御)。
    /// </remarks>
    private List<string> CollectDescendants(string path)
    {
        var result = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { path };
        var queue = new Queue<string>();
        queue.Enqueue(path);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (_nodes.TryGetValue(current, out var node))
            {
                foreach (var child in node.ChildPaths)
                {
                    if (visited.Add(child))
                    {
                        result.Add(child);
                        queue.Enqueue(child);
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 按路径段通配规则匹配路径集合。
    /// </summary>
    private IEnumerable<string> MatchPattern(string pattern)
    {
        var patternSegments = pattern.Split(':');
        return _nodes.Keys.Where(path => MatchesPattern(path.Split(':'), patternSegments));
    }

    /// <summary>
    /// 判断单一路径是否匹配模式路径。
    /// </summary>
    private static bool MatchesPattern(string[] pathSegments, string[] patternSegments)
    {
        if (patternSegments.Length > pathSegments.Length)
            return false;

        for (var i = 0; i < patternSegments.Length; i++)
        {
            if (patternSegments[i] != "*" && patternSegments[i] != pathSegments[i])
                return false;
        }

        return true;
    }

    /// <summary>
    /// 规范化路径字符串。
    /// </summary>
    private static string NormalizePath(string path)
    {
        return path.Trim().Trim(':');
    }

    /// <summary>
    /// 将路径解析为路径段数组。
    /// </summary>
    private static string[] ParsePathSegments(string path)
    {
        return path.Split(':', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// 获取路径的根节点段。
    /// </summary>
    private static string GetRootPath(string path)
    {
        var index = path.IndexOf(':');
        return index > 0 ? path[..index] : path;
    }

    /// <summary>
    /// 估算缓存项大小，用于统计信息聚合。
    /// </summary>
    /// <remarks>
    /// 委托给注入的 <see cref="ISizeEstimator"/>。默认 <see cref="DefaultSizeEstimator"/>
    /// 对未知类型返回 0，表示"不参与 Size 统计"——这与原生
    /// Microsoft.Extensions.Caching.Memory 的契约一致：调用方若关心容量，
    /// 应当通过 <c>MemoryCacheEntryOptions.Size</c> 显式设置。
    /// </remarks>
    private long EstimateSize<T>(T value)
    {
        return _sizeEstimator.EstimateSize(value);
    }

    /// <summary>
    /// 底层缓存驱逐回调，保持树索引与实际缓存一致。
    /// </summary>
    /// <remarks>
    /// 只在"真删除"场景下清理树节点:
    ///   - Expired / Capacity / TokenExpired → 清
    ///   - Replaced → 不清(同 key 新 entry 即将替代,树节点还要给新 entry 用)
    ///   - Removed → 不清(显式 Remove 路径已通过 RemoveInternal 同步了树索引)
    /// </remarks>
    private void OnCacheEntryEvicted(object key, object? value, EvictionReason reason, object? state)
    {
        if (key is string path && IsRealEviction(reason))
        {
            _structureLock.EnterWriteLock();
            try
            {
                RemoveSingleNode(path);
            }
            finally
            {
                _structureLock.ExitWriteLock();
            }
        }
    }

    /// <summary>
    /// 判断是否"真删除"——原缓存项不再存活,树索引应同步清理。
    /// Replaced 和 Removed 不算"真删除",因为新 entry 或显式 Remove 路径会处理。
    /// </summary>
    private static bool IsRealEviction(EvictionReason reason)
    {
        return reason is EvictionReason.Expired
            or EvictionReason.Capacity
            or EvictionReason.TokenExpired;
    }

    /// <summary>
    /// 在单个写锁范围内执行批量操作。
    /// </summary>
    internal void ExecuteBatch(List<BatchOperation> operations)
    {
        _structureLock.EnterWriteLock();
        try
        {
            foreach (var op in operations)
            {
                switch (op.Type)
                {
                    case OperationType.Set:
                        if (op.Value is not null)
                        {
                            var normalizedPath = NormalizePath(op.Path);
                            var segments = ParsePathSegments(normalizedPath);
                            var options = op.Options ?? new MemoryCacheEntryOptions();
                            // 直接调用 SetTreeUnderLock,避免"持锁调 SetTree"递归持锁
                            var entry = SetTreeUnderLock(normalizedPath, segments, op.Value, op.Tag, options);
                            entry.Dispose();
                        }
                        break;
                    case OperationType.Remove:
                        RemoveInternal(op.Path);
                        break;
                    case OperationType.RemoveTree:
                        var descendants = CollectDescendants(op.Path);
                        descendants.Insert(0, op.Path);
                        foreach (var p in descendants)
                        {
                            RemoveInternal(p);
                        }
                        break;
                }
            }
        }
        finally
        {
            _structureLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// 内部统计收集器。
    /// </summary>
    private sealed class CacheStatisticsCollector
    {
        private long _hitCount;
        private long _missCount;
        private long _cascadeDeleteCount;
        private long _totalAccessTimeTicks;
        private long _accessCount;

        public long HitCount => _hitCount;
        public long MissCount => _missCount;
        public long CascadeDeleteCount => _cascadeDeleteCount;

        public TimeSpan AverageAccessTime =>
            _accessCount > 0 ? TimeSpan.FromTicks(_totalAccessTimeTicks / _accessCount) : TimeSpan.Zero;

        public void RecordHit(TimeSpan duration)
        {
            Interlocked.Increment(ref _hitCount);
            Interlocked.Add(ref _totalAccessTimeTicks, duration.Ticks);
            Interlocked.Increment(ref _accessCount);
        }

        public void RecordMiss()
        {
            Interlocked.Increment(ref _missCount);
        }

        public void RecordCascadeDelete(int count, TimeSpan duration)
        {
            Interlocked.Increment(ref _cascadeDeleteCount);
            Interlocked.Add(ref _totalAccessTimeTicks, duration.Ticks);
            Interlocked.Increment(ref _accessCount);
        }

        public void RecordSet()
        {
        }
    }
}

/// <summary>
/// 批量操作类型,用于 <see cref="TreeCacheBatch"/> 内每个操作的语义区分。
/// </summary>
public enum OperationType
{
    /// <summary>
    /// 写入或覆盖一个缓存项,需要 <see cref="BatchOperation.Value"/>。
    /// </summary>
    Set,

    /// <summary>
    /// 删除单个节点(不含后代)。
    /// </summary>
    Remove,

    /// <summary>
    /// 级联删除整棵子树(含自身)。
    /// </summary>
    RemoveTree
}

/// <summary>
/// 批量操作模型,描述 <see cref="TreeCacheBatch.Execute"/> 时要执行的一条原子操作。
/// </summary>
public sealed class BatchOperation
{
    /// <summary>
    /// 操作类型,决定 <see cref="Value"/>、<see cref="Options"/>、<see cref="Tag"/> 哪些字段有效。
    /// </summary>
    public required OperationType Type { get; init; }

    /// <summary>
    /// 目标路径。
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// 写入的值(仅 <see cref="OperationType.Set"/> 使用)。
    /// </summary>
    public object? Value { get; init; }

    /// <summary>
    /// 内存缓存条目选项(仅 <see cref="OperationType.Set"/> 使用)。
    /// </summary>
    public MemoryCacheEntryOptions? Options { get; init; }

    /// <summary>
    /// 标签(仅 <see cref="OperationType.Set"/> 使用)。
    /// </summary>
    public string? Tag { get; init; }
}
