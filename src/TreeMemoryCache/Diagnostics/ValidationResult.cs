namespace TreeMemoryCache.Diagnostics;

/// <summary>
/// 验证结果的轻量级 DTO,通过 <see cref="TreeMemoryCache.Validate()"/> 获取。
/// </summary>
/// <remarks>
/// <see cref="IsValid"/> 仅反映 <see cref="Errors"/> 是否为空,警告(<see cref="Warnings"/>)
/// 不会让 <see cref="IsValid"/> 变为 false,但通常需要关注。
/// </remarks>
public sealed class ValidationResult
{
    /// <summary>
    /// 是否无错误。值为 <c>true</c> 当且仅当 <see cref="Errors"/> 为空。
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// 错误信息列表。出现错误时缓存结构已损坏,建议重建。
    /// </summary>
    public List<string> Errors { get; init; } = new();

    /// <summary>
    /// 警告信息列表。警告通常可恢复,例如孤立标签索引(下次访问会自动清理)。
    /// </summary>
    public List<string> Warnings { get; init; } = new();
}