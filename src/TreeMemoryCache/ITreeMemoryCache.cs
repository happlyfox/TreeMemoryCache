using Microsoft.Extensions.Caching.Memory;

namespace TreeMemoryCache;

/// <summary>
/// 提供树形路径缓存能力的接口，扩展 <see cref="IMemoryCache"/> 增加层级维护、
/// 级联删除、标签查询与诊断能力。
/// </summary>
/// <remarks>
/// <para>路径采用 ":" 分段(如 <c>A:B:C</c>)，支持通配符匹配(<c>*</c> 匹配单段)。</para>
/// <para>所有方法都是线程安全的：底层 <see cref="IMemoryCache"/> 自身支持并发，
/// 树索引通过 <see cref="System.Threading.ReaderWriterLockSlim"/> 保护。</para>
/// <para>注意：建议通过 <see cref="ITreeMemoryCache.SetTree{T}(string, T, MemoryCacheEntryOptions?)"/>
/// 等显式 API 操作缓存，直接调用 <see cref="IMemoryCache.CreateEntry(object)"/> 会绕过
/// 树索引维护，可能导致树结构与实际缓存不一致。</para>
/// </remarks>
public interface ITreeMemoryCache : IMemoryCache, IDisposable
{
    /// <summary>
    /// 删除指定路径下的子树节点。
    /// </summary>
    /// <param name="path">目标路径(根路径或中间路径均可)。</param>
    /// <param name="options">删除行为选项，详见 <see cref="TreeRemoveOptions"/>。默认 <c>null</c>(级联删除含自身)。</param>
    /// <exception cref="ArgumentException">当 <paramref name="path"/> 为 null 或空字符串时抛出。</exception>
    /// <exception cref="ObjectDisposedException">当缓存已 Dispose 后调用时抛出。</exception>
    void RemoveTree(string path, TreeRemoveOptions? options = null);

    /// <summary>
    /// 按树路径读取缓存值并尝试转换为指定类型。
    /// </summary>
    /// <typeparam name="T">期望的返回值类型。</typeparam>
    /// <param name="path">缓存路径,会自动规范化(Trim 两端空格和 ":")。</param>
    /// <param name="value">命中时输出缓存值,未命中或类型不匹配时输出 <c>default</c>。</param>
    /// <returns>命中且类型匹配返回 <c>true</c>,否则返回 <c>false</c>。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="path"/> 为 null 时抛出。</exception>
    /// <exception cref="ArgumentException">当 <paramref name="path"/> 经规范化后为空字符串时抛出。</exception>
    /// <exception cref="ObjectDisposedException">当缓存已 Dispose 后调用时抛出。</exception>
    bool TryGetTree<T>(string path, out T? value);

    /// <summary>
    /// 按树路径写入缓存项。必须在拿到 <see cref="ICacheEntry"/> 后调用 <c>Dispose()</c> 提交。
    /// 推荐使用便捷扩展 <see cref="TreeMemoryCacheExtensions.SetTreeValue{T}(ITreeMemoryCache, string, T, MemoryCacheEntryOptions?)"/>。
    /// </summary>
    /// <typeparam name="T">缓存值的类型。</typeparam>
    /// <param name="path">缓存路径。</param>
    /// <param name="value">要写入的值。</param>
    /// <param name="options">内存缓存条目选项(过期时间、容量等)。默认 <c>null</c>。</param>
    /// <returns>可被 Dispose 的缓存条目。</returns>
    /// <exception cref="ArgumentException">当 <paramref name="path"/> 为 null 或空字符串时抛出。</exception>
    /// <exception cref="ObjectDisposedException">当缓存已 Dispose 后调用时抛出。</exception>
    ICacheEntry SetTree<T>(string path, T value, MemoryCacheEntryOptions? options = null);

    /// <summary>
    /// 按树路径写入缓存项,并指定标签用于按标签查询与删除。
    /// </summary>
    /// <param name="path">缓存路径。</param>
    /// <param name="value">要写入的值。</param>
    /// <param name="tag">标签字符串,<c>null</c> 表示无标签。</param>
    /// <param name="options">内存缓存条目选项。默认 <c>null</c>。</param>
    /// <returns>可被 Dispose 的缓存条目。</returns>
    /// <exception cref="ArgumentException">当 <paramref name="path"/> 为 null 或空字符串时抛出。</exception>
    /// <exception cref="ObjectDisposedException">当缓存已 Dispose 后调用时抛出。</exception>
    ICacheEntry SetTree<T>(string path, T value, string? tag, MemoryCacheEntryOptions? options = null);

    /// <summary>
    /// 获取指定路径的直接子路径集合(不含孙代及更深的后代)。
    /// </summary>
    /// <param name="path">父路径。</param>
    /// <returns>直接子路径集合,目标路径不存在时返回空集合。</returns>
    /// <exception cref="ArgumentException">当 <paramref name="path"/> 为 null 或空字符串时抛出。</exception>
    /// <exception cref="ObjectDisposedException">当缓存已 Dispose 后调用时抛出。</exception>
    IEnumerable<string> GetChildPaths(string path);

    /// <summary>
    /// 获取指定路径的全部后代路径(不含自身)。
    /// </summary>
    /// <param name="path">祖先路径。</param>
    /// <returns>所有后代路径的集合,广度优先遍历。</returns>
    /// <exception cref="ArgumentException">当 <paramref name="path"/> 为 null 或空字符串时抛出。</exception>
    /// <exception cref="ObjectDisposedException">当缓存已 Dispose 后调用时抛出。</exception>
    IEnumerable<string> GetDescendantPaths(string path);

    /// <summary>
    /// 根据通配符模式查询路径集合。
    /// </summary>
    /// <param name="pattern">通配符模式,支持 <c>*</c> 匹配单段(如 <c>A:*</c> 匹配第一段为 A 的所有路径),
    /// 支持多段通配(如 <c>A:B:*</c>)。无通配符时按精确路径匹配。</param>
    /// <returns>匹配模式的路径集合。</returns>
    /// <exception cref="ArgumentException">当 <paramref name="pattern"/> 为 null 或空字符串时抛出。</exception>
    /// <exception cref="ObjectDisposedException">当缓存已 Dispose 后调用时抛出。</exception>
    IEnumerable<string> GetPathsByPattern(string pattern);

    /// <summary>
    /// 根据标签查询路径集合。
    /// </summary>
    /// <param name="tag">要查询的标签。</param>
    /// <returns>标有该标签的所有路径集合,标签不存在时返回空集合。</returns>
    /// <exception cref="ArgumentException">当 <paramref name="tag"/> 为 null 或空字符串时抛出。</exception>
    /// <exception cref="ObjectDisposedException">当缓存已 Dispose 后调用时抛出。</exception>
    IEnumerable<string> GetPathsByTag(string tag);

    /// <summary>
    /// 获取缓存统计信息,包含条目数、容量、命中率、级联删除计数等。
    /// </summary>
    /// <returns>当前的 <see cref="CacheStatistics"/> 快照。</returns>
    /// <exception cref="ObjectDisposedException">当缓存已 Dispose 后调用时抛出。</exception>
    CacheStatistics GetStatistics();

    /// <summary>
    /// 以异步流方式删除子树,并在每个节点被删除时 yield 一次,用于在长删除过程中给调用方反馈进度。
    /// </summary>
    /// <param name="path">目标路径。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步可枚举的已删除路径序列。</returns>
    /// <exception cref="ArgumentException">当 <paramref name="path"/> 为 null 或空字符串时抛出。</exception>
    /// <exception cref="ObjectDisposedException">当缓存已 Dispose 后调用时抛出。</exception>
    IAsyncEnumerable<string> RemoveTreeAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建批量操作对象,可在单个写锁内执行多条 Set/Remove/RemoveTree 操作。
    /// </summary>
    /// <returns>新创建的 <see cref="TreeCacheBatch"/>,使用后必须调用 <c>Execute()</c> 或 Dispose。</returns>
    /// <exception cref="ObjectDisposedException">当缓存已 Dispose 后调用时抛出。</exception>
    TreeCacheBatch CreateBatch();

    /// <summary>
    /// 原子地删除指定标签关联的所有路径(单写锁内'快照+删除',避免与并发 SetTree 改 tag 产生竞态)。
    /// </summary>
    /// <param name="tag">要删除的标签。</param>
    /// <exception cref="ArgumentException">当 <paramref name="tag"/> 为 null 或空字符串时抛出。</exception>
    /// <exception cref="ObjectDisposedException">当缓存已 Dispose 后调用时抛出。</exception>
    void RemoveByTag(string tag);
}
