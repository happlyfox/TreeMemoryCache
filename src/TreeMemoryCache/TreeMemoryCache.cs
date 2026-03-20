using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace TreeMemoryCache;

/// <summary>
/// 基于 MemoryCache 的树形缓存实现，支持路径层级维护与级联删除。
/// </summary>
public sealed class TreeMemoryCache : ITreeMemoryCache
{
    private readonly MemoryCache _innerCache;
    private readonly ConcurrentDictionary<string, CacheNode> _nodes;
    private readonly ConcurrentDictionary<string, HashSet<string>> _tagIndex;
    private readonly ReaderWriterLockSlim _structureLock;
    private readonly ILogger<TreeMemoryCache>? _logger;
    private readonly CacheStatisticsCollector _statistics;
    private bool _disposed;

    /// <summary>
    /// 初始化 TreeMemoryCache 实例。
    /// </summary>
    public TreeMemoryCache(MemoryCacheOptions? options = null, ILogger<TreeMemoryCache>? logger = null)
    {
        _innerCache = new MemoryCache(options ?? new MemoryCacheOptions());
        _nodes = new ConcurrentDictionary<string, CacheNode>(StringComparer.Ordinal);
        _tagIndex = new ConcurrentDictionary<string, HashSet<string>>(StringComparer.Ordinal);
        _structureLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        _logger = logger;
        _statistics = new CacheStatisticsCollector();
    }

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
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
            RemoveSingleNode(path);
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
                node.Size = EstimateSize(value);
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

            _statistics.RecordSet();
            return entry;
        }
        finally
        {
            _structureLock.ExitWriteLock();
        }
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
                RemoveSingleNode(nodePath);
                _innerCache.Remove(nodePath);
                count++;

                options.OnProgress?.Invoke(count);
            }

            stopwatch.Stop();
            _statistics.RecordCascadeDelete(count, stopwatch.Elapsed);
            _logger?.LogInformation("Cascade delete completed: {Path}, {Count} nodes removed in {Duration}ms",
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
        var pathsToRemove = new List<string>();

        _structureLock.EnterReadLock();
        try
        {
            pathsToRemove = CollectDescendants(normalizedPath);
            pathsToRemove.Insert(0, normalizedPath);
        }
        finally
        {
            _structureLock.ExitReadLock();
        }

        foreach (var nodePath in pathsToRemove)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _structureLock.EnterWriteLock();
            try
            {
                RemoveSingleNode(nodePath);
                _innerCache.Remove(nodePath);
            }
            finally
            {
                _structureLock.ExitWriteLock();
            }

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

        return pattern switch
        {
            "*" => _nodes.Keys.ToList(),
            "*:*" => _nodes.Keys.Where(k => k.Contains(':')).ToList(),
            _ when pattern.Contains('*') => MatchPattern(pattern),
            _ => _nodes.Keys.Where(k => k == pattern).ToList()
        };
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

        var paths = GetPathsByTag(tag).ToList();
        foreach (var path in paths)
        {
            RemoveTree(path);
        }
        _tagIndex.TryRemove(tag, out _);
    }

    /// <inheritdoc />
    public CacheStatistics GetStatistics()
    {
        EnsureNotDisposed();
        var nodeCountByRoot = new Dictionary<string, long>();
        long totalNodeCount = 0;
        long totalCacheSize = 0;

        foreach (var (path, node) in _nodes)
        {
            if (!_innerCache.TryGetValue(path, out _))
            {
                continue;
            }

            totalNodeCount++;
            totalCacheSize += node.Size;

            var root = GetRootPath(path);
            nodeCountByRoot.TryGetValue(root, out var count);
            nodeCountByRoot[root] = count + 1;
        }

        return new CacheStatistics
        {
            TotalNodeCount = totalNodeCount,
            TotalCacheSize = totalCacheSize,
            HitCount = _statistics.HitCount,
            MissCount = _statistics.MissCount,
            CascadeDeleteCount = _statistics.CascadeDeleteCount,
            AverageAccessTime = _statistics.AverageAccessTime,
            NodeCountByRoot = nodeCountByRoot
        };
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
        if (_disposed) return;

        _structureLock.EnterWriteLock();
        try
        {
            _innerCache.Dispose();
            _nodes.Clear();
            _tagIndex.Clear();
            _disposed = true;
        }
        finally
        {
            _structureLock.ExitWriteLock();
            _structureLock.Dispose();
        }
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
    /// 删除单个节点并修复父子引用与标签索引。
    /// </summary>
    private void RemoveSingleNode(string path)
    {
        if (_nodes.TryRemove(path, out var node))
        {
            if (node.ParentPath is not null && _nodes.TryGetValue(node.ParentPath, out var parentNode))
            {
                parentNode.ChildPaths.Remove(path);
            }

            foreach (var childPath in node.ChildPaths.ToList())
            {
                if (_nodes.TryGetValue(childPath, out var childNode))
                {
                    childNode.ParentPath = node.ParentPath;
                }
            }

            if (node.Tag is not null && _tagIndex.TryGetValue(node.Tag, out var taggedPaths))
            {
                taggedPaths.Remove(path);
            }
        }
    }

    /// <summary>
    /// 广度优先收集指定路径下的全部后代路径。
    /// </summary>
    private List<string> CollectDescendants(string path)
    {
        var result = new List<string>();
        var queue = new Queue<string>();
        queue.Enqueue(path);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (_nodes.TryGetValue(current, out var node))
            {
                foreach (var child in node.ChildPaths)
                {
                    result.Add(child);
                    queue.Enqueue(child);
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
    private static long EstimateSize<T>(T value)
    {
        return value switch
        {
            string s => s.Length * 2,
            byte[] bytes => bytes.Length,
            Array array => array.Length * 8,
            _ => 100
        };
    }

    /// <summary>
    /// 底层缓存驱逐回调，保持树索引与实际缓存一致。
    /// </summary>
    private void OnCacheEntryEvicted(object key, object? value, EvictionReason reason, object? state)
    {
        if (key is string path && reason != EvictionReason.Removed)
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
                            var entry = SetTree(op.Path, op.Value, op.Tag, op.Options);
                            entry.Dispose();
                        }
                        break;
                    case OperationType.Remove:
                        RemoveSingleNode(op.Path);
                        _innerCache.Remove(op.Path);
                        break;
                    case OperationType.RemoveTree:
                        var descendants = CollectDescendants(op.Path);
                        descendants.Insert(0, op.Path);
                        foreach (var p in descendants)
                        {
                            RemoveSingleNode(p);
                            _innerCache.Remove(p);
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
/// 批量操作类型。
/// </summary>
public enum OperationType
{
    Set,
    Remove,
    RemoveTree
}

/// <summary>
/// 批量操作模型。
/// </summary>
public sealed class BatchOperation
{
    public required OperationType Type { get; init; }
    public required string Path { get; init; }
    public object? Value { get; init; }
    public MemoryCacheEntryOptions? Options { get; init; }
    public string? Tag { get; init; }
}
