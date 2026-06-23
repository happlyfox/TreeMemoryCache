namespace TreeMemoryCache;

/// <summary>
/// 自定义缓存项大小估算器,通过 <see cref="TreeMemoryCache"/> 构造函数注入。
/// </summary>
/// <remarks>
/// TreeMemoryCache 不强制估算缓存项大小(等同 .NET 原生
/// <c>Microsoft.Extensions.Caching.Memory</c> 约定:调用方负责 Size)。
/// 但允许调用方通过 <see cref="ISizeEstimator"/> 注入全局估算策略。
/// </remarks>
public interface ISizeEstimator
{
    /// <summary>
    /// 估算给定缓存项的大小(字节)。
    /// </summary>
    /// <typeparam name="T">值类型。</typeparam>
    /// <param name="value">要估算的值。</param>
    /// <returns>估算的字节数;返回 0 表示"不参与大小统计"。</returns>
    long EstimateSize<T>(T value);
}

/// <summary>
/// 默认 Size 估算器:对已知类型做精确估算,对未知类型返回 0(不参与统计)。
/// </summary>
public sealed class DefaultSizeEstimator : ISizeEstimator
{
    /// <summary>
    /// 无状态默认实现,可作为单例复用,避免每次 new TreeMemoryCache 时分配实例。
    /// </summary>
    public static readonly DefaultSizeEstimator Instance = new();

    private DefaultSizeEstimator() { }

    /// <inheritdoc />
    public long EstimateSize<T>(T value) => value switch
    {
        string s => s.Length * 2,
        byte[] bytes => bytes.Length,
        Array array => array.Length * IntPtr.Size,
        _ => 0
    };
}