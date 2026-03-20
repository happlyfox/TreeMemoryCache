namespace TreeMemoryCache;

/// <summary>
/// 控制树形删除行为的选项。
/// </summary>
public sealed class TreeRemoveOptions
{
    /// <summary>
    /// 是否删除输入路径本身，默认为 true。
    /// </summary>
    public bool IncludeSelf { get; set; } = true;
    /// <summary>
    /// 预留的异步标记位。
    /// </summary>
    public bool Async { get; set; }
    /// <summary>
    /// 删除进度回调，参数为当前已删除节点数量。
    /// </summary>
    public Action<int>? OnProgress { get; set; }
    /// <summary>
    /// 删除超时时间，默认 30 秒。
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
