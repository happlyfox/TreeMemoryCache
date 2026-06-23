using Microsoft.Extensions.Caching.Memory;

namespace TreeMemoryCache;

/// <summary>
/// TreeMemoryCache 的便捷扩展方法集合。
/// </summary>
public static class TreeMemoryCacheExtensions
{
    /// <summary>
    /// 按树路径写入缓存项,自动完成 entry 的 Dispose 提交。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="cache">目标缓存实例。</param>
    /// <param name="path">缓存路径。</param>
    /// <param name="value">要写入的值。</param>
    /// <param name="options">内存缓存条目选项。</param>
    /// <remarks>
    /// 这是 <see cref="ITreeMemoryCache.SetTree{T}(string, T, MemoryCacheEntryOptions?)"/>
    /// 的便捷封装,无需显式调用 <c>Dispose()</c>。
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
    /// 按树路径写入缓存项并指定标签,自动完成 entry 的 Dispose 提交。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="cache">目标缓存实例。</param>
    /// <param name="path">缓存路径。</param>
    /// <param name="value">要写入的值。</param>
    /// <param name="tag">关联标签,<c>null</c> 表示无标签。</param>
    /// <param name="options">内存缓存条目选项。</param>
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
