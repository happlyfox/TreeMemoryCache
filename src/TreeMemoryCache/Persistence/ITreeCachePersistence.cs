namespace TreeMemoryCache.Persistence;

/// <summary>
/// 树形缓存持久化器接口,定义将缓存项序列化到外部存储的标准 API。
/// </summary>
/// <remarks>
/// <para>TreeMemoryCache 内置 <see cref="JsonFilePersistence"/> 实现,
/// 调用方可实现本接口对接 Redis/数据库/对象存储等后端。</para>
/// <para>实现应当是**幂等**的:同一缓存快照多次 Save 应当产生相同结果。</para>
/// </remarks>
public interface ITreeCachePersistence
{
    /// <summary>
    /// 获取持久化策略模式(同步/异步/惰性)。
    /// </summary>
    PersistenceStrategy Strategy { get; }

    /// <summary>
    /// 是否处于启用状态。被禁用的持久化器的 Save/Load 会成为 no-op。
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// 上次成功保存的时间(UTC),未保存过则为 <c>null</c>。
    /// </summary>
    DateTimeOffset? LastSavedAt { get; }

    /// <summary>
    /// 同步保存(阻塞当前线程直到完成)。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已保存的节点数量。</returns>
    int Save(CancellationToken cancellationToken = default);

    /// <summary>
    /// 同步加载(阻塞当前线程直到完成)。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已加载的节点数量。</returns>
    int Load(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步保存。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已保存的节点数量。</returns>
    Task<int> SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步加载。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已加载的节点数量。</returns>
    Task<int> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 标记指定路径为脏(仅对 <see cref="PersistenceStrategy.Asynchronous"/> 有效),
    /// 用于延迟批量写入。
    /// </summary>
    /// <param name="path">发生变化的路径。</param>
    void MarkDirty(string path);

    /// <summary>
    /// 强制将所有脏数据刷入存储(仅对 <see cref="PersistenceStrategy.Asynchronous"/> 有效)。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示异步刷写完成的 <see cref="Task"/>。</returns>
    Task FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查底层存储是否存在(用于 Load 前的快速判断)。
    /// </summary>
    /// <returns>存在返回 <c>true</c>,否则 <c>false</c>。</returns>
    bool Exists();

    /// <summary>
    /// 获取存储元数据(节点数、文件大小、创建时间等),存储不存在时返回 <c>null</c>。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>存储元数据,或 <c>null</c>。</returns>
    ValueTask<StorageMetadata?> GetMetadataAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 存储元数据,描述持久化文件的整体状态。
/// </summary>
public sealed class StorageMetadata
{
    /// <summary>
    /// 存储中缓存项的数量。
    /// </summary>
    public int NodeCount { get; init; }

    /// <summary>
    /// 存储文件/对象的创建时间(UTC)。
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// 存储占用的字节数。
    /// </summary>
    public long SizeBytes { get; init; }
}