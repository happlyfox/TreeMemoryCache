namespace TreeMemoryCache.Persistence;

/// <summary>
/// 持久化策略模式
/// </summary>
public enum PersistenceStrategy
{
    /// <summary>
    /// 同步写入：每次操作后立即保存
    /// </summary>
    Synchronous,

    /// <summary>
    /// 异步写入：批量延迟保存
    /// </summary>
    Asynchronous,

    /// <summary>
    /// 惰性写入：仅在 Dispose 或显式调用时保存
    /// </summary>
    Lazy
}
