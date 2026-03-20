using Microsoft.Extensions.Caching.Memory;

namespace TreeMemoryCache;

/// <summary>
/// 提供树形路径缓存能力，支持层级查询和级联删除。
/// </summary>
public interface ITreeMemoryCache : IMemoryCache, IDisposable
{
    /// <summary>
    /// 删除指定路径下的子树节点。
    /// </summary>
    void RemoveTree(string path, TreeRemoveOptions? options = null);
    /// <summary>
    /// 按树路径读取缓存值并尝试转换为指定类型。
    /// </summary>
    bool TryGetTree<T>(string path, out T? value);
    /// <summary>
    /// 按树路径写入缓存项。
    /// </summary>
    ICacheEntry SetTree<T>(string path, T value, MemoryCacheEntryOptions? options = null);
    /// <summary>
    /// 按树路径写入缓存项，并指定标签。
    /// </summary>
    ICacheEntry SetTree<T>(string path, T value, string? tag, MemoryCacheEntryOptions? options = null);
    /// <summary>
    /// 获取指定路径的直接子路径。
    /// </summary>
    IEnumerable<string> GetChildPaths(string path);
    /// <summary>
    /// 获取指定路径的全部后代路径。
    /// </summary>
    IEnumerable<string> GetDescendantPaths(string path);
    /// <summary>
    /// 根据通配符模式查询路径集合。
    /// </summary>
    IEnumerable<string> GetPathsByPattern(string pattern);
    /// <summary>
    /// 根据标签查询路径集合。
    /// </summary>
    IEnumerable<string> GetPathsByTag(string tag);
    /// <summary>
    /// 获取缓存统计信息。
    /// </summary>
    CacheStatistics GetStatistics();
    /// <summary>
    /// 以异步流方式删除子树，并返回每个被删除的路径。
    /// </summary>
    IAsyncEnumerable<string> RemoveTreeAsync(string path, CancellationToken cancellationToken = default);
    /// <summary>
    /// 创建批量操作对象。
    /// </summary>
    TreeCacheBatch CreateBatch();
    /// <summary>
    /// 删除指定标签关联的所有路径。
    /// </summary>
    void RemoveByTag(string tag);
}
