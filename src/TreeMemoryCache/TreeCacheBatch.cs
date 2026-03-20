using Microsoft.Extensions.Caching.Memory;

namespace TreeMemoryCache;

/// <summary>
/// 封装树形缓存的批量操作，确保只执行一次。
/// </summary>
public sealed class TreeCacheBatch : IDisposable
{
    private readonly TreeMemoryCache _cache;
    private readonly List<BatchOperation> _operations = new();
    private bool _executed;
    private bool _disposed;

    internal TreeCacheBatch(TreeMemoryCache cache)
    {
        _cache = cache;
    }

    /// <summary>
    /// 添加一条写入操作。
    /// </summary>
    public TreeCacheBatch Set<T>(string path, T value, MemoryCacheEntryOptions? options = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ObjectDisposedException.ThrowIf(_executed, this);
        _operations.Add(new BatchOperation { Type = OperationType.Set, Path = path, Value = value, Options = options, Tag = null });
        return this;
    }

    /// <summary>
    /// 添加一条带标签的写入操作。
    /// </summary>
    public TreeCacheBatch Set<T>(string path, T value, string? tag, MemoryCacheEntryOptions? options = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ObjectDisposedException.ThrowIf(_executed, this);
        _operations.Add(new BatchOperation { Type = OperationType.Set, Path = path, Value = value, Options = options, Tag = tag });
        return this;
    }

    /// <summary>
    /// 添加一条单节点删除操作。
    /// </summary>
    public TreeCacheBatch Remove(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ObjectDisposedException.ThrowIf(_executed, this);
        _operations.Add(new BatchOperation { Type = OperationType.Remove, Path = path });
        return this;
    }

    /// <summary>
    /// 添加一条子树删除操作。
    /// </summary>
    public TreeCacheBatch RemoveTree(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ObjectDisposedException.ThrowIf(_executed, this);
        _operations.Add(new BatchOperation { Type = OperationType.RemoveTree, Path = path });
        return this;
    }

    /// <summary>
    /// 提交并执行批量操作。
    /// </summary>
    public void Execute()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ObjectDisposedException.ThrowIf(_executed, this);
        _cache.ExecuteBatch(_operations);
        _executed = true;
    }

    /// <summary>
    /// 释放批量对象并清空待执行操作。
    /// </summary>
    public void Dispose()
    {
        _disposed = true;
        _operations.Clear();
    }
}
