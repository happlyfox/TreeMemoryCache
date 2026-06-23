namespace TreeMemoryCache.Diagnostics;

public sealed class CacheDiagnostics
{
    public int TotalNodes { get; init; }
    public int DeadParentLinks { get; init; }
    public long EstimatedMemoryBytes { get; init; }
    public Dictionary<string, int> TagDistribution { get; init; } = new();
    public List<string> DeepestPaths { get; init; } = new();
}
