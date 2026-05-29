using Microsoft.Extensions.Caching.Memory;

namespace TreeMemoryCache;

/// <summary>
/// TreeMemoryCache 的扩展方法，提供更便捷的 API。
/// </summary>
public static class TreeMemoryCacheExtensions
{
    /// <summary>
    /// 按树路径写入缓存项，自动完成提交。
    /// </summary>
    /// <remarks>
    /// 这是 SetTree 的便捷封装，开发者无需显式调用 Dispose()。
    /// </remarks>
    public static void SetTreeValue<T>(
        this ITreeMemoryCache cache,
        string path,
        T value,
        MemoryCacheEntryOptions? options = null)
    {
        cache.SetTree(path, value, options).Dispose();
    }

    /// <summary>
    /// 按树路径写入缓存项并指定标签，自动完成提交。
    /// </summary>
    public static void SetTreeValue<T>(
        this ITreeMemoryCache cache,
        string path,
        T value,
        string? tag,
        MemoryCacheEntryOptions? options = null)
    {
        cache.SetTree(path, value, tag, options).Dispose();
    }
}
