namespace TreeMemoryCache;

/// <summary>
/// 控制树形删除行为的选项,通过 <see cref="ITreeMemoryCache.RemoveTree"/> 传入。
/// </summary>
public sealed class TreeRemoveOptions
{
    /// <summary>
    /// 是否删除输入路径本身,默认为 <c>true</c>。
    /// </summary>
    /// <remarks>
    /// 设为 <c>false</c> 时,只删除后代,保留输入节点自身。
    /// </remarks>
    public bool IncludeSelf { get; set; } = true;

    /// <summary>
    /// 是否将子节点孤儿化(不删除子节点,改为断开父子关系),默认为 <c>false</c>(级联删除所有后代)。
    /// </summary>
    /// <remarks>
    /// 当为 <c>true</c> 时,只删除指定路径本身,其子节点会被断开父子关系但保留。
    /// </remarks>
    public bool OrphanChildren { get; set; } = false;

    /// <summary>
    /// 删除进度回调,参数为当前已删除节点数量。
    /// </summary>
    /// <remarks>
    /// 每次删除一个节点后触发,适合用于 UI 进度显示或长时间删除的反馈。
    /// </remarks>
    public Action<int>? OnProgress { get; set; }

    /// <summary>
    /// 删除超时时间,默认 30 秒。
    /// </summary>
    /// <remarks>
    /// 当前作为语义保留字段,实际删除操作未硬性遵循该超时(异步路径可通过取消令牌替代)。
    /// </remarks>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
