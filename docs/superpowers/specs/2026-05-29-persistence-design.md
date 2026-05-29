# TreeMemoryCache 持久化功能设计方案

## 概述

为 TreeMemoryCache 添加持久化支持，通过抽象接口让用户自定义存储实现。

## 架构设计

```
┌─────────────────────────────────────────────────────────┐
│                    ITreeMemoryCache                     │
└─────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────┐
│              ITreeCachePersistence (接口)               │
│  - 抽象接口，让用户自定义实现                           │
│  - 三种策略：同步/异步/惰性                            │
└─────────────────────────────────────────────────────────┘
                            │
            ┌───────────────┼───────────────┐
            ▼               ▼               ▼
    ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
    │  JsonFile  │  │   SQLite   │  │  自定义实现 │
    │ Persistence│  │ Persistence│  │  (用户)    │
    └─────────────┘  └─────────────┘  └─────────────┘
```

## 持久化策略

| 策略 | 说明 | 一致性 | 性能 |
|------|------|--------|------|
| Synchronous | 每次操作后立即保存 | 高 | 低 |
| Asynchronous | 批量延迟保存 | 中 | 高 |
| Lazy | 仅在 Dispose/Save 时保存 | 低 | 高 |

## 持久化数据

| 字段 | 类型 | 说明 |
|------|------|------|
| Path | string | 完整路径 |
| ParentPath | string? | 父节点路径 |
| ChildPaths | List<string> | 子节点集合 |
| Tag | string? | 标签 |
| CreatedAt | DateTimeOffset | 创建时间 |
| ExpiresAt | DateTimeOffset? | 过期时间 |
| Value | object? | 缓存值 |

## 新增文件结构

```
src/TreeMemoryCache/
├── Persistence/
│   ├── ITreeCachePersistence.cs      # 持久化接口
│   ├── PersistenceStrategy.cs        # 策略枚举
│   ├── CacheNodeSnapshot.cs           # 节点快照
│   └── JsonFilePersistence.cs         # JSON 文件实现
├── TreeMemoryCache.cs                # 添加持久化构造函数
└── ITreeMemoryCache.cs               # 可选：添加持久化扩展接口
```

## 接口定义

### ITreeCachePersistence

```csharp
public enum PersistenceStrategy
{
    Synchronous,    // 同步写入
    Asynchronous,    // 异步批量写入
    Lazy            // 惰性写入
}

public interface ITreeCachePersistence
{
    PersistenceStrategy Strategy { get; }
    bool IsEnabled { get; }
    DateTimeOffset? LastSavedAt { get; }

    // 同步操作
    int Save(CancellationToken cancellationToken = default);
    int Load(CancellationToken cancellationToken = default);

    // 异步操作
    Task<int> SaveAsync(CancellationToken cancellationToken = default);
    Task<int> LoadAsync(CancellationToken cancellationToken = default);

    // 脏标记（异步模式）
    void MarkDirty(string path);
    Task FlushAsync(CancellationToken cancellationToken = default);

    // 工具
    bool Exists();
    Task<StorageMetadata?> GetMetadataAsync(CancellationToken cancellationToken = default);
}

public sealed class StorageMetadata
{
    public int NodeCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public long SizeBytes { get; init; }
}
```

### CacheNodeSnapshot

```csharp
public sealed class CacheNodeSnapshot
{
    public required string Path { get; init; }
    public string? ParentPath { get; init; }
    public List<string> ChildPaths { get; init; } = new();
    public string? Tag { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public long Size { get; init; }
    public object? Value { get; init; }
}
```

## TreeMemoryCache 构造函数

```csharp
public sealed class TreeMemoryCache : ITreeMemoryCache
{
    // 原有构造函数保持不变
    public TreeMemoryCache(MemoryCacheOptions? options = null,
        ILogger<TreeMemoryCache>? logger = null);

    // 新增：带持久化的构造函数
    public TreeMemoryCache(
        ITreeCachePersistence? persistence = null,
        MemoryCacheOptions? options = null,
        ILogger<TreeMemoryCache>? logger = null);

    // 新增：持久化属性
    public ITreeCachePersistence? Persistence { get; }

    // 新增：保存/加载方法
    public Task<int> SaveAsync(CancellationToken cancellationToken = default);
    public Task<int> LoadAsync(CancellationToken cancellationToken = default);
}
```

## JsonFilePersistence 实现

```csharp
public sealed class JsonFilePersistence : ITreeCachePersistence
{
    public JsonFilePersistence(
        string filePath,
        PersistenceStrategy strategy = PersistenceStrategy.Synchronous);

    public PersistenceStrategy Strategy { get; }
    public bool IsEnabled { get; }
    public DateTimeOffset? LastSavedAt { get; }

    public Task<int> SaveAsync(CancellationToken cancellationToken = default);
    public Task<int> LoadAsync(CancellationToken cancellationToken = default);

    public void MarkDirty(string path);
    public Task FlushAsync(CancellationToken cancellationToken = default);
    public bool Exists();
}
```

## 使用示例

```csharp
// ============ 同步模式（热备份）===========
var cache = new TreeMemoryCache(
    new JsonFilePersistence("cache/tree.json", PersistenceStrategy.Synchronous));

cache.SetTreeValue("users:1", user);  // 自动保存
cache.SetTreeValue("users:2", user2);  // 自动保存

// ============ 异步模式（冷备份）===========
var cache = new TreeMemoryCache(
    new JsonFilePersistence("cache/snapshot.json", PersistenceStrategy.Asynchronous));

cache.SetTreeValue("data:large", bigData);  // 标记脏
cache.SetTreeValue("data:more", moreData); // 标记脏
// 批量保存
await cache.Persistence!.FlushAsync();

// ============ 惰性模式 ============
var cache = new TreeMemoryCache(
    new JsonFilePersistence("cache/lazy.json", PersistenceStrategy.Lazy));

cache.SetTreeValue("temp:data", tempData);
// 仅在需要时保存
await cache.SaveAsync();

// ============ 启动时加载 ============
var cache = new TreeMemoryCache(
    new JsonFilePersistence("cache/tree.json"));

if (cache.Persistence!.Exists())
{
    await cache.LoadAsync();
}
```

## 测试策略

1. **单元测试**：JsonFilePersistence 的序列化和反序列化
2. **集成测试**：保存-重启-加载流程
3. **策略测试**：三种策略的行为验证

## 兼容性

- 原有构造函数和 API 完全不变
- 持久化为可选功能，不启用时不影响性能
- 接口设计允许用户实现 Redis、数据库等自定义持久化
