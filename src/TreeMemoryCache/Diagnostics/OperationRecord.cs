using TreeMemoryCache;

namespace TreeMemoryCache.Diagnostics;

public sealed class OperationRecord
{
    public DateTimeOffset Timestamp { get; init; }
    public OperationType Type { get; init; }
    public string Path { get; init; } = string.Empty;
    public string? Tag { get; init; }
    public string? CallerMemberName { get; init; }
    public string? CallerFilePath { get; init; }
    public int CallerLineNumber { get; init; }
}
