using TreeMemoryCache;

namespace TreeMemoryCache.Diagnostics;

/// <summary>
/// 单次缓存操作的追踪记录,用于审计与诊断。
/// </summary>
/// <remarks>
/// 由 <see cref="ITreeMemoryCache.GetOperationHistory"/> 返回。
/// 容量上限 1000 条,超出后按 FIFO 淘汰,生产环境不应依赖此 API 做业务判断。
/// </remarks>
public sealed class OperationRecord
{
    /// <summary>
    /// 操作发生的时间戳(UTC)。
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// 操作类型(参见 <see cref="OperationType"/>)。
    /// </summary>
    public OperationType Type { get; init; }

    /// <summary>
    /// 操作涉及的缓存路径。
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// 操作涉及的标签(<see cref="OperationType.Set"/> 时有效)。
    /// </summary>
    public string? Tag { get; init; }

    /// <summary>
    /// 调用方的方法名(<c>CallerMemberName</c> 自动填充)。
    /// </summary>
    public string? CallerMemberName { get; init; }

    /// <summary>
    /// 调用方所在的源文件路径(<c>CallerFilePath</c> 自动填充)。
    /// </summary>
    public string? CallerFilePath { get; init; }

    /// <summary>
    /// 调用方所在的源文件行号(<c>CallerLineNumber</c> 自动填充)。
    /// </summary>
    public int CallerLineNumber { get; init; }
}