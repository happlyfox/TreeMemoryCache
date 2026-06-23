using Microsoft.Extensions.Logging;

namespace TreeMemoryCache.Logging;

internal static class StructuredLoggers
{
    private static readonly Action<ILogger, string, int, long, Exception?> LogCascadeDelete =
        LoggerMessage.Define<string, int, long>(
            LogLevel.Information,
            0,
            "级联删除完成: {Path}, 删除 {Count} 个节点, 耗时 {DurationMs}ms");

    private static readonly Action<ILogger, string, Exception?> s_logCacheHit =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            1,
            "缓存命中: {Path}");

    private static readonly Action<ILogger, string, Exception?> s_logCacheMiss =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            2,
            "缓存未命中: {Path}");

    public static void LogCascadeDeleteCompleted(ILogger? logger, string path, int count, long durationMs)
    {
        LogCascadeDelete(logger, path, count, durationMs, null);
    }

    public static void LogCacheHit(ILogger? logger, string path)
    {
        s_logCacheHit(logger, path, null);
    }

    public static void LogCacheMiss(ILogger? logger, string path)
    {
        s_logCacheMiss(logger, path, null);
    }
}
