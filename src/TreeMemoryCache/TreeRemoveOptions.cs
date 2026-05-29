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
    /// 是否将子节点孤儿化（不删除子节点，改为断开父子关系）。
    /// 默认为 false（级联删除所有后代）。
    /// </summary>
    /// <remarks>
    /// 当为 true 时，只删除指定路径本身，其子节点会被断开父子关系但保留。
    /// </remarks>
    public bool OrphanChildren { get; set; } = false;

    /// <summary>
    /// 删除进度回调，参数为当前已删除节点数量。
    /// </summary>
    public Action<int>? OnProgress { get; set; }

    /// <summary>
    /// 删除超时时间，默认 30 秒。
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
