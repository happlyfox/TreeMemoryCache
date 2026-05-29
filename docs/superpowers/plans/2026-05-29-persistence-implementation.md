# TreeMemoryCache 持久化功能实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 TreeMemoryCache 添加持久化支持，通过抽象接口让用户自定义存储实现

**Architecture:** 新增 ITreeCachePersistence 接口定义持久化契约，提供 JsonFilePersistence 作为默认实现，TreeMemoryCache 支持可选的持久化注入

**Tech Stack:** .NET 9, System.Text.Json, Microsoft.Extensions.Caching.Memory

---

## 文件结构

```
src/TreeMemoryCache/
├── Persistence/
│   ├── PersistenceStrategy.cs        # 策略枚举
│   ├── ITreeCachePersistence.cs      # 持久化接口
│   ├── CacheNodeSnapshot.cs          # 节点快照
│   └── JsonFilePersistence.cs         # JSON 文件实现
└── TreeMemoryCache.cs                # 添加持久化构造函数和方法
```

---

## Task 1: 创建持久化基础类型

**Files:**
- Create: `src/TreeMemoryCache/Persistence/PersistenceStrategy.cs`
- Create: `tests/TreeMemoryCache.Tests/PersistenceTests.cs`

- [ ] **Step 1: 创建 PersistenceStrategy.cs**

```csharp
namespace TreeMemoryCache.Persistence;

/// <summary>
/// 持久化策略模式
/// </summary>
public enum PersistenceStrategy
{
    /// <summary>
    /// 同步写入：每次操作后立即保存
    /// </summary>
    Synchronous,

    /// <summary>
    /// 异步写入：批量延迟保存
    /// </summary>
    Asynchronous,

    /// <summary>
    /// 惰性写入：仅在 Dispose 或显式调用时保存
    /// </summary>
    Lazy
}
```

- [ ] **Step 2: 创建 CacheNodeSnapshot.cs**

```csharp
namespace TreeMemoryCache.Persistence;

/// <summary>
/// 节点快照，用于序列化和反序列化
/// </summary>
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

- [ ] **Step 3: 创建 ITreeCachePersistence.cs**

```csharp
namespace TreeMemoryCache.Persistence;

/// <summary>
/// 树形缓存持久化接口
/// </summary>
public interface ITreeCachePersistence
{
    /// <summary>
    /// 当前持久化策略
    /// </summary>
    PersistenceStrategy Strategy { get; }

    /// <summary>
    /// 是否已启用持久化
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// 上次保存时间
    /// </summary>
    DateTimeOffset? LastSavedAt { get; }

    /// <summary>
    /// 同步保存所有数据
    /// </summary>
    int Save(CancellationToken cancellationToken = default);

    /// <summary>
    /// 同步加载数据
    /// </summary>
    int Load(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步保存所有数据
    /// </summary>
    Task<int> SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步加载数据
    /// </summary>
    Task<int> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 标记路径已修改（异步模式）
    /// </summary>
    void MarkDirty(string path);

    /// <summary>
    /// 刷新待处理的变更（异步模式）
    /// </summary>
    Task FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查是否存在可加载的数据
    /// </summary>
    bool Exists();

    /// <summary>
    /// 获取存储元信息
    /// </summary>
    ValueTask<StorageMetadata?> GetMetadataAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 存储元数据
/// </summary>
public sealed class StorageMetadata
{
    public int NodeCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public long SizeBytes { get; init; }
}
```

- [ ] **Step 4: 编译验证**

Run: `dotnet build`
Expected: 成功编译

- [ ] **Step 5: 提交**

```bash
git add src/TreeMemoryCache/Persistence/
git commit -m "feat: 添加持久化基础类型 (PersistenceStrategy, CacheNodeSnapshot, ITreeCachePersistence)"
```

---

## Task 2: 创建 JsonFilePersistence 实现

**Files:**
- Create: `src/TreeMemoryCache/Persistence/JsonFilePersistence.cs`
- Modify: `tests/TreeMemoryCache.Tests/PersistenceTests.cs`

- [ ] **Step 1: 创建 JsonFilePersistence.cs**

```csharp
using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace TreeMemoryCache.Persistence;

/// <summary>
/// JSON 文件持久化实现
/// </summary>
public sealed class JsonFilePersistence : ITreeCachePersistence
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ConcurrentDictionary<string, DateTime> _dirtyPaths = new();
    private readonly bool _enabled;

    public PersistenceStrategy Strategy { get; }
    public bool IsEnabled => _enabled;
    public DateTimeOffset? LastSavedAt { get; private set; }
    public int PendingChanges => _dirtyPaths.Count;

    public JsonFilePersistence(
        string filePath,
        PersistenceStrategy strategy = PersistenceStrategy.Synchronous)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        _filePath = filePath;
        Strategy = strategy;
        _enabled = true;

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public int Save(CancellationToken cancellationToken = default)
    {
        return SaveAsync(cancellationToken).GetAwaiter().GetResult();
    }

    public async Task<int> SaveAsync(CancellationToken cancellationToken = default)
    {
        return 0; // 占位，实现见 Task 4
    }

    public int Load(CancellationToken cancellationToken = default)
    {
        return LoadAsync(cancellationToken).GetAwaiter().GetResult();
    }

    public async Task<int> LoadAsync(CancellationToken cancellationToken = default)
    {
        return 0; // 占位，实现见 Task 4
    }

    public void MarkDirty(string path)
    {
        if (Strategy == PersistenceStrategy.Asynchronous)
        {
            _dirtyPaths[path] = DateTime.UtcNow;
        }
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (Strategy == PersistenceStrategy.Asynchronous && _dirtyPaths.Count > 0)
        {
            return SaveAsync(cancellationToken);
        }
        return Task.CompletedTask;
    }

    public bool Exists() => File.Exists(_filePath);

    public async ValueTask<StorageMetadata?> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        if (!Exists())
            return null;

        var fileInfo = new FileInfo(_filePath);
        return new StorageMetadata
        {
            NodeCount = 0, // 占位
            CreatedAt = fileInfo.CreationTimeUtc,
            SizeBytes = fileInfo.Length
        };
    }
}
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build`
Expected: 成功编译

- [ ] **Step 3: 提交**

```bash
git add src/TreeMemoryCache/Persistence/JsonFilePersistence.cs
git commit -m "feat: 添加 JsonFilePersistence 骨架"
```

---

## Task 3: 修改 TreeMemoryCache 添加持久化支持

**Files:**
- Modify: `src/TreeMemoryCache/TreeMemoryCache.cs` (添加构造函数和持久化方法)
- Create: `src/TreeMemoryCache/Persistence/PersistenceExtensions.cs` (扩展方法)

- [ ] **Step 1: 添加持久化构造函数和方法到 TreeMemoryCache.cs**

在 TreeMemoryCache 类中添加：

```csharp
private readonly ITreeCachePersistence? _persistence;

public ITreeCachePersistence? Persistence => _persistence;

public TreeMemoryCache(
    ITreeCachePersistence? persistence = null,
    MemoryCacheOptions? options = null,
    ILogger<TreeMemoryCache>? logger = null)
    : this(options, logger)
{
    _persistence = persistence;
}

public async Task<int> SaveAsync(CancellationToken cancellationToken = default)
{
    if (_persistence == null || !_persistence.IsEnabled)
        return 0;

    var snapshots = GetAllSnapshots();
    return await _persistence.SaveAsync(cancellationToken);
}

public async Task<int> LoadAsync(CancellationToken cancellationToken = default)
{
    if (_persistence == null || !_persistence.IsEnabled)
        return 0;

    if (!_persistence.Exists())
        return 0;

    return await _persistence.LoadAsync(cancellationToken);
}

internal List<CacheNodeSnapshot> GetAllSnapshots()
{
    var snapshots = new List<CacheNodeSnapshot>();

    _structureLock.EnterReadLock();
    try
    {
        foreach (var (path, node) in _nodes)
        {
            _innerCache.TryGetValue(path, out var value);

            snapshots.Add(new CacheNodeSnapshot
            {
                Path = node.Path,
                ParentPath = node.ParentPath,
                ChildPaths = node.ChildPaths.ToList(),
                Tag = node.Tag,
                CreatedAt = node.CreatedAt,
                ExpiresAt = node.ExpiresAt,
                Size = node.Size,
                Value = value
            });
        }
    }
    finally
    {
        _structureLock.ExitReadLock();
    }

    return snapshots;
}

internal void RestoreFromSnapshots(IEnumerable<CacheNodeSnapshot> snapshots)
{
    _structureLock.EnterWriteLock();
    try
    {
        foreach (var snapshot in snapshots)
        {
            // 重建 CacheNode
            var node = new CacheNode
            {
                Path = snapshot.Path,
                ParentPath = snapshot.ParentPath,
                ChildPaths = new HashSet<string>(snapshot.ChildPaths, StringComparer.Ordinal),
                CreatedAt = snapshot.CreatedAt,
                ExpiresAt = snapshot.ExpiresAt,
                Size = snapshot.Size,
                Tag = snapshot.Tag
            };

            _nodes[snapshot.Path] = node;

            // 重建父节点引用
            if (snapshot.ParentPath != null && _nodes.TryGetValue(snapshot.ParentPath, out var parentNode))
            {
                parentNode.ChildPaths.Add(snapshot.Path);
            }

            // 重建缓存值
            if (snapshot.Value != null)
            {
                var entry = _innerCache.CreateEntry(snapshot.Path);
                entry.Value = snapshot.Value;
                if (snapshot.ExpiresAt.HasValue)
                    entry.AbsoluteExpiration = snapshot.ExpiresAt.Value;
                entry.Dispose();
            }

            // 重建标签索引
            if (!string.IsNullOrEmpty(snapshot.Tag))
            {
                var taggedPaths = _tagIndex.GetOrAdd(snapshot.Tag, _ => new HashSet<string>(StringComparer.Ordinal));
                taggedPaths.Add(snapshot.Path);
            }
        }
    }
    finally
    {
        _structureLock.ExitWriteLock();
    }
}
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build`
Expected: 成功编译

- [ ] **Step 3: 提交**

```bash
git add src/TreeMemoryCache/TreeMemoryCache.cs
git commit -m "feat: TreeMemoryCache 添加持久化构造函数和 SaveAsync/LoadAsync 方法"
```

---

## Task 4: 完成 JsonFilePersistence 完整实现

**Files:**
- Modify: `src/TreeMemoryCache/Persistence/JsonFilePersistence.cs`

- [ ] **Step 1: 编写失败的测试**

```csharp
[Fact]
public async Task JsonPersistence_SaveAndLoad_ShouldRestoreData()
{
    var tempFile = Path.GetTempFileName();
    try
    {
        var persistence = new JsonFilePersistence(tempFile, PersistenceStrategy.Synchronous);

        // 保存测试数据
        using var cache = new TreeMemoryCache(persistence);
        cache.SetTreeValue("A:B:C", "test-value");
        cache.SetTreeValue("A:B:C", "test-value", "test-tag");

        await persistence.SaveAsync();

        // 重新加载
        using var cache2 = new TreeMemoryCache(persistence);
        await persistence.LoadAsync();

        Assert.True(cache2.TryGetTree<string>("A:B:C", out var value));
        Assert.Equal("test-value", value);
    }
    finally
    {
        File.Delete(tempFile);
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test --filter "JsonPersistence_SaveAndLoad_ShouldRestoreData"`
Expected: FAIL

- [ ] **Step 3: 实现完整的 SaveAsync**

```csharp
public async Task<int> SaveAsync(CancellationToken cancellationToken = default)
{
    if (!_enabled)
        return 0;

    // 获取快照
    var snapshots = new List<CacheNodeSnapshot>();

    // 从缓存收集数据（通过事件回调或直接访问）
    OnSnapshotRequested?.Invoke(this, snapshots);

    var json = JsonSerializer.Serialize(snapshots, _jsonOptions);

    // 原子写入
    var tempPath = _filePath + ".tmp";
    await File.WriteAllTextAsync(tempPath, json, cancellationToken);

    if (File.Exists(_filePath))
        File.Delete(_filePath);
    File.Move(tempPath, _filePath);

    _dirtyPaths.Clear();
    LastSavedAt = DateTimeOffset.UtcNow;

    return snapshots.Count;
}
```

- [ ] **Step 4: 实现完整的 LoadAsync**

```csharp
public async Task<int> LoadAsync(CancellationToken cancellationToken = default)
{
    if (!_enabled || !Exists())
        return 0;

    var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
    var snapshots = JsonSerializer.Deserialize<List<CacheNodeSnapshot>>(json, _jsonOptions);

    if (snapshots == null || snapshots.Count == 0)
        return 0;

    OnRestoreRequested?.Invoke(this, snapshots);

    return snapshots.Count;
}
```

- [ ] **Step 5: 添加事件用于收集快照**

```csharp
public event EventHandler<List<CacheNodeSnapshot>>? OnSnapshotRequested;
public event EventHandler<List<CacheNodeSnapshot>>? OnRestoreRequested;
```

- [ ] **Step 6: 在 TreeMemoryCache 中触发事件**

在 SetTree 方法中：
```csharp
if (_persistence?.Strategy == PersistenceStrategy.Synchronous)
{
    // 同步保存
}
else if (_persistence?.Strategy == PersistenceStrategy.Asynchronous)
{
    _persistence.MarkDirty(normalizedPath);
}
```

- [ ] **Step 7: 运行测试验证**

Run: `dotnet test --filter "JsonPersistence"`
Expected: PASS

- [ ] **Step 8: 提交**

```bash
git add src/TreeMemoryCache/Persistence/JsonFilePersistence.cs src/TreeMemoryCache/TreeMemoryCache.cs
git commit -m "feat: 完成 JsonFilePersistence 完整实现"
```

---

## Task 5: 添加持久化集成测试

**Files:**
- Modify: `tests/TreeMemoryCache.Tests/PersistenceTests.cs`

- [ ] **Step 1: 添加完整测试**

```csharp
[Fact]
public async Task SynchronousStrategy_ShouldSaveImmediately()
{
    var tempFile = Path.GetTempFileName();
    try
    {
        var persistence = new JsonFilePersistence(tempFile, PersistenceStrategy.Synchronous);
        using var cache = new TreeMemoryCache(persistence);

        cache.SetTreeValue("test", "value");

        // 同步模式下，值应该立即保存
        Assert.True(persistence.Exists());
    }
    finally { File.Delete(tempFile); }
}

[Fact]
public async Task AsynchronousStrategy_ShouldRequireFlush()
{
    var tempFile = Path.GetTempFileName();
    try
    {
        var persistence = new JsonFilePersistence(tempFile, PersistenceStrategy.Asynchronous);
        using var cache = new TreeMemoryCache(persistence);

        cache.SetTreeValue("test", "value");

        // 异步模式，需要手动 Flush
        Assert.False(persistence.Exists());

        await persistence.FlushAsync();
        Assert.True(persistence.Exists());
    }
    finally { File.Delete(tempFile); }
}

[Fact]
public async Task Load_ShouldRestoreTreeStructure()
{
    var tempFile = Path.GetTempFileName();
    try
    {
        // 创建并保存
        var persistence = new JsonFilePersistence(tempFile);
        using (var cache = new TreeMemoryCache(persistence))
        {
            cache.SetTreeValue("root:child1:leaf", "v1");
            cache.SetTreeValue("root:child2", "v2");
            cache.SetTreeValue("root:child2", "v2", "my-tag");
            await persistence.SaveAsync();
        }

        // 重新加载
        using (var cache2 = new TreeMemoryCache(persistence))
        {
            await persistence.LoadAsync();

            Assert.True(cache2.TryGetTree<string>("root:child1:leaf", out _));
            Assert.True(cache2.TryGetTree<string>("root:child2", out _));

            var children = cache2.GetChildPaths("root").ToList();
            Assert.Equal(2, children.Count);
        }
    }
    finally { File.Delete(tempFile); }
}
```

- [ ] **Step 2: 运行所有持久化测试**

Run: `dotnet test --filter "PersistenceTests"`
Expected: 全部 PASS

- [ ] **Step 3: 提交**

```bash
git add tests/TreeMemoryCache.Tests/PersistenceTests.cs
git commit -m "test: 添加持久化集成测试"
```

---

## Task 6: 最终验证

**Files:**
- Run: 完整测试套件

- [ ] **Step 1: 运行完整测试套件**

Run: `dotnet test`
Expected: 全部 PASS

- [ ] **Step 2: 更新示例**

在 `samples/TreeMemoryCache.QuickStart/Program.cs` 添加持久化示例：

```csharp
using var cache = new TreeMemoryCache(
    new JsonFilePersistence("cache/tree.json"));

// 启动时加载
await cache.LoadAsync();

// 正常使用
cache.SetTreeValue("Line:6:Upward:Stations", stations);

// 保存
await cache.SaveAsync();
```

- [ ] **Step 3: 最终提交**

```bash
git add -A
git commit -m "feat: 完成持久化功能开发"
```

---

## 实现检查清单

- [ ] Task 1: 持久化基础类型
- [ ] Task 2: JsonFilePersistence 骨架
- [ ] Task 3: TreeMemoryCache 持久化支持
- [ ] Task 4: JsonFilePersistence 完整实现
- [ ] Task 5: 持久化集成测试
- [ ] Task 6: 最终验证
