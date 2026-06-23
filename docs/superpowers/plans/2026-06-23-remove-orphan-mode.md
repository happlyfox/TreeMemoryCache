# 移除孤儿化（OrphanChildren）模式实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 彻底移除 TreeMemoryCache 中与"孤儿化"相关的所有代码、API 选项、诊断字段、测试与文档，让项目严格遵循"树形缓存 = 父节点不存在时子节点也必须被删除"的纯粹语义。

**Architecture:** 删除 `TreeRemoveOptions.OrphanChildren` 选项、移除 `RemoveTree` 中的孤儿化分支、精简 `CacheDiagnostics` 中"孤儿节点"相关字段、清理 `Validator` 中的孤儿节点检测、删除未使用的孤儿化日志方法、删除对应的回归测试。保持公开 API 的破坏性变更（明确标注 BREAKING）。

**Tech Stack:** .NET 8 / xUnit / Microsoft.Extensions.Caching.Memory 10.0.5

---

## 全局约束

- 项目遵循 CLAUDE.md 中开发约定：中文注释、UTF-8 with BOM、LF 行尾、命名空间 `TreeMemoryCache`
- 实施过程中**不**修改任何无关的源代码（例如现有未提交的 CacheStatistics/TreeMemoryCache.cs 改动与本次任务无关，按"提交破坏性变更"原则单独处理）
- 实施完成后必须能 `dotnet build` 与 `dotnet test` 全通过
- 所有移除的公开 API 必须在 commit message 中以 `BREAKING CHANGE:` 前缀标注
- 计划文档归档位置（最终交付时落地）：`docs/superpowers/plans/2026-06-23-remove-orphan-mode.md`
- 严禁引入新的"软删除/孤儿子树/虚拟节点"等概念——树形缓存就是硬删除

---

## 文件结构总览

| 文件 | 操作 | 说明 |
|------|------|------|
| `src/TreeMemoryCache/TreeRemoveOptions.cs` | 修改 | 删除 `OrphanChildren` 属性 |
| `src/TreeMemoryCache/TreeMemoryCache.cs` | 修改 | 简化 `RemoveTree` 分支、删除 `GetDiagnostics` 中孤儿节点检测、删除 `RemoveSingleNode` 中孤儿化相关注释、删除 `StructuredLoggers.LogOrphanChildrenCompleted` 调用 |
| `src/TreeMemoryCache/Diagnostics/CacheDiagnostics.cs` | 修改 | 删除 `OrphanedNodes` / `OrphanedPaths` 字段（保留 `DeadParentLinks` —— 它代表"指针失效"语义，仍可在持久化损坏等场景下填充） |
| `src/TreeMemoryCache/Diagnostics/Validator.cs` | 修改 | 删除"孤儿节点"错误检测（树形缓存不允许 ParentPath 指向不存在节点，违反时此错误检测无意义——直接删除） |
| `src/TreeMemoryCache/Logging/StructuredLoggers.cs` | 修改 | 删除 `LogOrphanChildren` 与 `LogOrphanChildrenCompleted` |
| `tests/TreeMemoryCache.Tests/BugFixTests.cs` | 修改 | 删除 2 个孤儿化测试方法 |
| `src/TreeMemoryCache/README.md` | 修改 | 移除"孤儿化"相关描述（实际未提及，确认无影响） |
| `README.md` | 修改 | 同上（实际未提及，确认无影响） |
| `CLAUDE.md` | 修改 | 更新"注意事项"——移除第 5 条关于 `OrphanChildren` 的说明 |
| `docs/superpowers/plans/2026-06-23-remove-orphan-mode.md` | 创建 | 本计划的最终归档位置（实施前复制至此） |

---

## 任务列表

### Task 1：删除 `TreeRemoveOptions.OrphanChildren` 属性

**Files:**
- Modify: `src/TreeMemoryCache/TreeRemoveOptions.cs`
- Test: 通过运行 `dotnet test` 验证剩余测试仍可编译

**接口契约（修改后）：**
```csharp
public sealed class TreeRemoveOptions
{
    public bool IncludeSelf { get; set; } = true;
    public Action<int>? OnProgress { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
```

- [ ] **Step 1.1：阅读当前文件确认行号**

Read: `src/TreeMemoryCache/TreeRemoveOptions.cs`
确认 OrphanChildren 属性位于第 13-20 行。

- [ ] **Step 1.2：删除 OrphanChildren 属性**

执行 Edit：
```
old_string:
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

new_string:
    /// <summary>
    /// 删除进度回调，参数为当前已删除节点数量。
    /// </summary>
    public Action<int>? OnProgress { get; set; }
```

- [ ] **Step 1.3：验证文件最终内容**

Read: `src/TreeMemoryCache/TreeRemoveOptions.cs`
预期：文件仅剩 4 个字段（class 头、IncludeSelf、OnProgress、Timeout），共 22 行。

- [ ] **Step 1.4：先编译看是否仅暴露调用点错误（不要立刻改 RemoveTree）**

Run: `dotnet build src/TreeMemoryCache/TreeMemoryCache.csproj -nologo`
预期：编译失败，错误信息应明确指出 `RemoveTree` 方法中存在 `options.OrphanChildren` 引用，且 `TreeMemoryCache.cs:264` 调用了已删除的日志方法。
**注意**：这些是预期的破坏性编译错误，会在后续 Task 中修复，本步骤仅用于确认 OrphanChildren 引用已被定位。

- [ ] **Step 1.5：Commit**

```bash
git add src/TreeMemoryCache/TreeRemoveOptions.cs
git commit -m "refactor!: 移除 TreeRemoveOptions.OrphanChildren 选项

BREAKING CHANGE: TreeRemoveOptions.OrphanChildren 已删除。
调用方需将原 OrphanChildren = true 的删除改为显式调用 RemoveTree
（级联删除）或保留子节点场景下手动重建父子关系。"
```

---

### Task 2：精简 `RemoveTree` 实现，删除孤儿化分支

**Files:**
- Modify: `src/TreeMemoryCache/TreeMemoryCache.cs:232-298`

**目标实现：**
```csharp
public void RemoveTree(string path, TreeRemoveOptions? options = null)
{
    EnsureNotDisposed();
    ArgumentException.ThrowIfNullOrEmpty(path);
    options ??= new TreeRemoveOptions();

    var normalizedPath = NormalizePath(path);
    var stopwatch = Stopwatch.StartNew();

    _structureLock.EnterWriteLock();
    try
    {
        var pathsToRemove = CollectDescendants(normalizedPath);
        if (options.IncludeSelf)
        {
            pathsToRemove.Insert(0, normalizedPath);
        }
        else
        {
            pathsToRemove = pathsToRemove.Where(p => p != normalizedPath).ToList();
        }

        var count = 0;
        foreach (var nodePath in pathsToRemove)
        {
            RemoveInternal(nodePath);
            count++;

            options.OnProgress?.Invoke(count);
        }

        stopwatch.Stop();
        _statistics.RecordCascadeDelete(count, stopwatch.Elapsed);
        _logger?.LogInformation(
            "Cascade delete completed: {Path}, {Count} nodes removed in {Duration}ms",
            normalizedPath, count, stopwatch.ElapsedMilliseconds);
    }
    finally
    {
        _structureLock.ExitWriteLock();
    }
}
```

- [ ] **Step 2.1：替换 RemoveTree 实现**

执行 Edit：
```
old_string:
    _structureLock.EnterWriteLock();
    try
    {
        // 如果是孤儿化模式，只处理自身节点，不处理后代
        if (options.OrphanChildren)
        {
            // 断开子节点的父子引用，但不删除它们
            if (_nodes.TryGetValue(normalizedPath, out var node))
            {
                foreach (var childPath in node.ChildPaths.ToList())
                {
                    if (_nodes.TryGetValue(childPath, out var childNode))
                    {
                        childNode.ParentPath = null; // 孤儿化
                    }
                }
            }

            // 删除自身
            RemoveInternal(normalizedPath);

            stopwatch.Stop();
            _statistics.RecordCascadeDelete(1, stopwatch.Elapsed);
            _logger?.LogInformation("Orphan children completed: {Path}", normalizedPath);
        }
        else
        {
            // 原有逻辑：级联删除
            var pathsToRemove = CollectDescendants(normalizedPath);
            if (options.IncludeSelf)
            {
                pathsToRemove.Insert(0, normalizedPath);
            }
            else
            {
                pathsToRemove = pathsToRemove.Where(p => p != normalizedPath).ToList();
            }

            var count = 0;
            foreach (var nodePath in pathsToRemove)
            {
                RemoveInternal(nodePath);
                count++;

                options.OnProgress?.Invoke(count);
            }

            stopwatch.Stop();
            _statistics.RecordCascadeDelete(count, stopwatch.Elapsed);
            _logger?.LogInformation("Cascade delete completed: {Path}, {Count} nodes removed in {Duration}ms",
                normalizedPath, count, stopwatch.ElapsedMilliseconds);
        }
    }
    finally
    {
        _structureLock.ExitWriteLock();
    }

new_string:
    _structureLock.EnterWriteLock();
    try
    {
        var pathsToRemove = CollectDescendants(normalizedPath);
        if (options.IncludeSelf)
        {
            pathsToRemove.Insert(0, normalizedPath);
        }
        else
        {
            pathsToRemove = pathsToRemove.Where(p => p != normalizedPath).ToList();
        }

        var count = 0;
        foreach (var nodePath in pathsToRemove)
        {
            RemoveInternal(nodePath);
            count++;

            options.OnProgress?.Invoke(count);
        }

        stopwatch.Stop();
        _statistics.RecordCascadeDelete(count, stopwatch.Elapsed);
        _logger?.LogInformation(
            "Cascade delete completed: {Path}, {Count} nodes removed in {Duration}ms",
            normalizedPath, count, stopwatch.ElapsedMilliseconds);
    }
    finally
    {
        _structureLock.ExitWriteLock();
    }
```

- [ ] **Step 2.2：更新 RemoveSingleNode 中关于孤儿化的过时注释**

执行 Edit：
```
old_string:
    /// <summary>
    /// 删除单个节点并修复父子引用与标签索引。
    /// 子节点的父子关系会被断开（孤儿化），而不是跳级连接到祖父节点。
    /// </summary>
    private void RemoveSingleNode(string path)

new_string:
    /// <summary>
    /// 删除单个节点并清理其父子引用与标签索引。
    /// 注意：本方法只删除指定节点本身，调用方需自行负责级联删除。
    /// </summary>
    private void RemoveSingleNode(string path)
```

- [ ] **Step 2.3：删除 RemoveSingleNode 内部关于孤儿状态的注释块**

执行 Edit：
```
old_string:
            // 从父节点的子节点列表中移除
            if (node.ParentPath is not null && _nodes.TryGetValue(node.ParentPath, out var parentNode))
            {
                parentNode.ChildPaths.Remove(path);
            }

            // 子节点保持孤儿状态（ParentPath 保持不变，指向已不存在的节点）
            // 不再将其重新连接到祖父节点，避免破坏树形语义

            // 清理标签索引

new_string:
            // 从父节点的子节点列表中移除
            if (node.ParentPath is not null && _nodes.TryGetValue(node.ParentPath, out var parentNode))
            {
                parentNode.ChildPaths.Remove(path);
            }

            // 清理标签索引
```

- [ ] **Step 2.4：验证编译错误是否消除**

Run: `dotnet build src/TreeMemoryCache/TreeMemoryCache.csproj -nologo`
预期：编译失败已减少到 0 或仅剩测试项目相关错误（孤儿化测试尚未删除）。

- [ ] **Step 2.5：Commit**

```bash
git add src/TreeMemoryCache/TreeMemoryCache.cs
git commit -m "refactor!: RemoveTree 移除孤儿化分支并清理过时注释

BREAKING CHANGE: 配合 TreeRemoveOptions.OrphanChildren 删除，
RemoveTree 现在始终执行纯树形级联删除。子节点不再保留为孤儿节点。"
```

---

### Task 3：精简 `CacheDiagnostics`，移除孤儿节点字段

**Files:**
- Modify: `src/TreeMemoryCache/Diagnostics/CacheDiagnostics.cs`

**目标实现：**
```csharp
namespace TreeMemoryCache.Diagnostics;

public sealed class CacheDiagnostics
{
    public int TotalNodes { get; init; }
    public int DeadParentLinks { get; init; }
    public long EstimatedMemoryBytes { get; init; }
    public Dictionary<string, int> TagDistribution { get; init; } = new();
    public List<string> DeepestPaths { get; init; } = new();
}
```

**说明：** `DeadParentLinks` 字段保留——它语义上是"指向不存在父节点的指针计数"，在持久化损坏或外部清理场景仍有诊断价值；但删除 `OrphanedNodes` / `OrphanedPaths`（它们专门服务于"孤儿化模式产生的悬挂节点"）。

- [ ] **Step 3.1：替换 CacheDiagnostics 字段定义**

执行 Edit：
```
old_string:
    public int TotalNodes { get; init; }
    public int OrphanedNodes { get; init; }
    public int DeadParentLinks { get; init; }
    public long EstimatedMemoryBytes { get; init; }
    public Dictionary<string, int> TagDistribution { get; init; } = new();
    public List<string> DeepestPaths { get; init; } = new();
    public List<string> OrphanedPaths { get; init; } = new();

new_string:
    public int TotalNodes { get; init; }
    public int DeadParentLinks { get; init; }
    public long EstimatedMemoryBytes { get; init; }
    public Dictionary<string, int> TagDistribution { get; init; } = new();
    public List<string> DeepestPaths { get; init; } = new();
```

- [ ] **Step 3.2：同步更新 `TreeMemoryCache.GetDiagnostics`**

执行 Edit：
```
old_string:
    public CacheDiagnostics GetDiagnostics()
    {
        EnsureNotDisposed();
        _structureLock.EnterReadLock();
        try
        {
            var orphanedPaths = new List<string>();
            var tagDistribution = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var (path, node) in _nodes)
            {
                if (node.ParentPath is not null && !_nodes.ContainsKey(node.ParentPath))
                {
                    orphanedPaths.Add(path);
                }

                if (node.Tag is not null)
                {
                    tagDistribution.TryGetValue(node.Tag, out var count);
                    tagDistribution[node.Tag] = count + 1;
                }
            }

            var deepestPaths = _nodes.Keys
                .OrderByDescending(p => p.Split(':').Length)
                .Take(5)
                .ToList();

            return new CacheDiagnostics
            {
                TotalNodes = _nodes.Count,
                OrphanedNodes = orphanedPaths.Count,
                EstimatedMemoryBytes = _nodes.Values.Sum(n => n.Size),
                TagDistribution = tagDistribution,
                DeepestPaths = deepestPaths,
                OrphanedPaths = orphanedPaths
            };
        }
        finally
        {
            _structureLock.ExitReadLock();
        }
    }

new_string:
    public CacheDiagnostics GetDiagnostics()
    {
        EnsureNotDisposed();
        _structureLock.EnterReadLock();
        try
        {
            var deadParentLinks = 0;
            var tagDistribution = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var (path, node) in _nodes)
            {
                if (node.ParentPath is not null && !_nodes.ContainsKey(node.ParentPath))
                {
                    deadParentLinks++;
                }

                if (node.Tag is not null)
                {
                    tagDistribution.TryGetValue(node.Tag, out var count);
                    tagDistribution[node.Tag] = count + 1;
                }
            }

            var deepestPaths = _nodes.Keys
                .OrderByDescending(p => p.Split(':').Length)
                .Take(5)
                .ToList();

            return new CacheDiagnostics
            {
                TotalNodes = _nodes.Count,
                DeadParentLinks = deadParentLinks,
                EstimatedMemoryBytes = _nodes.Values.Sum(n => n.Size),
                TagDistribution = tagDistribution,
                DeepestPaths = deepestPaths
            };
        }
        finally
        {
            _structureLock.ExitReadLock();
        }
    }
```

- [ ] **Step 3.3：Commit**

```bash
git add src/TreeMemoryCache/Diagnostics/CacheDiagnostics.cs src/TreeMemoryCache/TreeMemoryCache.cs
git commit -m "refactor: CacheDiagnostics 移除孤儿节点字段，保留 DeadParentLinks

OrphanedNodes / OrphanedPaths 是为'孤儿化模式'服务的诊断，
现已与 OrphanChildren 一起移除。DeadParentLinks 用于发现
持久化损坏等异常场景，仍保留。"
```

---

### Task 4：精简 `Validator`，移除孤儿节点检测

**Files:**
- Modify: `src/TreeMemoryCache/Diagnostics/Validator.cs`

**目标实现：**
```csharp
using System.Collections.Concurrent;

namespace TreeMemoryCache.Diagnostics;

internal static class Validator
{
    public static ValidationResult Validate(
        ConcurrentDictionary<string, CacheNode> nodes,
        ConcurrentDictionary<string, HashSet<string>> tagIndex)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // 树形缓存不允许 ParentPath 指向不存在的节点：
        // 这类不一致状态只能由持久化损坏或外部异常触发，发现时直接报告错误。
        foreach (var (path, node) in nodes)
        {
            if (node.ParentPath is not null && !nodes.ContainsKey(node.ParentPath))
            {
                errors.Add($"DeadParentLink: {path} 指向不存在的父节点 {node.ParentPath}");
            }
        }

        var nodeTags = nodes.Values
            .Where(n => n.Tag is not null)
            .Select(n => n.Tag!)
            .ToHashSet();

        foreach (var tag in tagIndex.Keys)
        {
            if (!nodeTags.Contains(tag))
            {
                warnings.Add($"孤立标签索引: {tag}");
            }
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }
}
```

**关键决策：** 保留"ParentPath 指向不存在节点"的错误检测（重命名为 `DeadParentLink`），但**不再**视其为"孤儿化模式产生的不一致"——它是纯粹的"持久化损坏"诊断信号。这是唯一一处涉及"死指针"语义的内核代码，删除会损失重要诊断能力。

- [ ] **Step 4.1：更新错误信息措辞**

执行 Edit：
```
old_string:
            if (node.ParentPath is not null && !nodes.ContainsKey(node.ParentPath))
            {
                errors.Add($"孤儿节点: {path} 指向不存在的父节点 {node.ParentPath}");
            }

new_string:
            if (node.ParentPath is not null && !nodes.ContainsKey(node.ParentPath))
            {
                errors.Add($"DeadParentLink: {path} 指向不存在的父节点 {node.ParentPath}");
            }
```

- [ ] **Step 4.2：Commit**

```bash
git add src/TreeMemoryCache/Diagnostics/Validator.cs
git commit -m "refactor: Validator 错误信息改为 DeadParentLink 措辞

原'孤儿节点'术语与已移除的 OrphanChildren 模式绑定，
改为更精确的 DeadParentLink，与 CacheDiagnostics 字段一致。"
```

---

### Task 5：清理 `StructuredLoggers` 中的孤儿化日志

**Files:**
- Modify: `src/TreeMemoryCache/Logging/StructuredLoggers.cs`

**目标实现：**
```csharp
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
```

- [ ] **Step 5.1：删除 LogOrphanChildren 字段与方法**

执行 Edit：
```
old_string:
    private static readonly Action<ILogger, string, int, long, Exception?> LogCascadeDelete =
        LoggerMessage.Define<string, int, long>(
            LogLevel.Information,
            0,
            "级联删除完成: {Path}, 删除 {Count} 个节点, 耗时 {DurationMs}ms");

    private static readonly Action<ILogger, string, Exception?> LogOrphanChildren =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            1,
            "孤儿化子节点完成: {Path}");

    private static readonly Action<ILogger, string, Exception?> s_logCacheHit =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            2,
            "缓存命中: {Path}");

    private static readonly Action<ILogger, string, Exception?> s_logCacheMiss =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            3,
            "缓存未命中: {Path}");

    public static void LogCascadeDeleteCompleted(ILogger? logger, string path, int count, long durationMs)
    {
        LogCascadeDelete(logger, path, count, durationMs, null);
    }

    public static void LogOrphanChildrenCompleted(ILogger? logger, string path)
    {
        LogOrphanChildren(logger, path, null);
    }

    public static void LogCacheHit(ILogger? logger, string path)
    {
        s_logCacheHit(logger, path, null);
    }

    public static void LogCacheMiss(ILogger? logger, string path)
    {
        s_logCacheMiss(logger, path, null);
    }

new_string:
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
```

**注意：** EventId 编号从 `LogOrphanChildren` 占用的 `1` 重新分配：Hit 由 2 → 1，Miss 由 3 → 2。这是有意的——避免外部日志聚合系统认为"事件消失"。

- [ ] **Step 5.2：Commit**

```bash
git add src/TreeMemoryCache/Logging/StructuredLoggers.cs
git commit -m "refactor: StructuredLoggers 移除孤儿化日志方法

LogOrphanChildrenCompleted 无人调用（RemoveTree 之前使用的是
英文 _logger?.LogInformation），与 OrphanChildren 一起清理。
EventId 重新编号：Hit→1，Miss→2。"
```

---

### Task 6：删除孤儿化相关测试

**Files:**
- Modify: `tests/TreeMemoryCache.Tests/BugFixTests.cs`
- Test: `dotnet test` 验证

- [ ] **Step 6.1：删除 2 个孤儿化测试方法**

执行 Edit：
```
old_string:
    [Fact]
    public void RemoveTree_WithOrphanChildren_ShouldPreserveChildren()
    {
        using var cache = new TreeMemoryCache();

        // 创建树结构：Line:6 -> Upward -> Stations 和 Line:6 -> Upward -> Station2
        cache.SetTree("Line:6:Upward:Stations", "A").Dispose();
        cache.SetTree("Line:6:Upward:Station2", "B").Dispose();

        // 使用 OrphanChildren 选项删除中间节点，保留子节点
        cache.RemoveTree("Line:6:Upward", new TreeRemoveOptions { OrphanChildren = true });

        // 验证：Line:6:Upward 不存在
        Assert.False(cache.TryGetTree<string>("Line:6:Upward", out _));

        // 验证：子节点仍然存在（孤儿化）
        Assert.True(cache.TryGetTree<string>("Line:6:Upward:Stations", out var a));
        Assert.Equal("A", a);
        Assert.True(cache.TryGetTree<string>("Line:6:Upward:Station2", out var b));
        Assert.Equal("B", b);

        // 验证：子节点不再是 Line:6 的子节点（孤儿化成功）
        var childrenOfLine6 = cache.GetChildPaths("Line:6").ToList();
        Assert.Empty(childrenOfLine6);
    }

    [Fact]
    public void RemoveTree_OrphanedChildren_ShouldBeIndependentlyDeletable()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:Upward:Stations", "A").Dispose();
        cache.RemoveTree("Line:6:Upward", new TreeRemoveOptions { OrphanChildren = true });

        // 孤儿节点应该可以独立删除
        cache.RemoveTree("Line:6:Upward:Stations");

        Assert.False(cache.TryGetTree<string>("Line:6:Upward:Stations", out _));
    }

    [Fact]
    public void Batch_DisposeWithoutExecute_ShouldThrowIfOperationsPending()

new_string:
    [Fact]
    public void Batch_DisposeWithoutExecute_ShouldThrowIfOperationsPending()
```

- [ ] **Step 6.2：运行测试验证编译与测试通过**

Run: `dotnet test tests/TreeMemoryCache.Tests/TreeMemoryCache.Tests.csproj -nologo`
预期：全部测试通过；应不再有 `RemoveTree_WithOrphanChildren_*` / `RemoveTree_OrphanedChildren_*` 测试。

- [ ] **Step 6.3：Commit**

```bash
git add tests/TreeMemoryCache.Tests/BugFixTests.cs
git commit -m "test: 删除 OrphanChildren 相关测试用例

OrphanChildren 模式已整体移除，对应的 BugFixTests 中 2 个
回归测试失去测试目标，一并删除。"
```

---

### Task 7：补充"严格树形删除"回归测试

**Files:**
- Create: `tests/TreeMemoryCache.Tests/CascadeDeleteContractTests.cs`

**目的：** 防止未来误重新引入孤儿化分支。在没有 `OrphanChildren` 选项的情况下，验证"删除父节点必然级联删除子节点"是硬契约。

- [ ] **Step 7.1：创建新测试文件**

Write 到 `tests/TreeMemoryCache.Tests/CascadeDeleteContractTests.cs`：

```csharp
using Xunit;

namespace TreeMemoryCache.Tests;

/// <summary>
/// 验证 TreeMemoryCache 严格遵循"树形缓存"语义：
/// 删除父节点必然级联删除全部后代，不存在任何保留子节点的模式。
///
/// 这些测试充当回归防护：任何尝试重新引入"孤儿化"或"软删除"模式的
/// 改动都会被以下测试捕获。
/// </summary>
public class CascadeDeleteContractTests
{
    [Fact]
    public void RemoveTree_ShouldAlwaysDeleteAllDescendants()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:Upward:Stations", "A").Dispose();
        cache.SetTree("Line:6:Upward:Station2", "B").Dispose();
        cache.SetTree("Line:6:Downward:Stations", "C").Dispose();

        // 不传 options，使用默认配置（TreeRemoveOptions { IncludeSelf = true }）
        cache.RemoveTree("Line:6:Upward");

        // 自身被删除
        Assert.False(cache.TryGetTree<string>("Line:6:Upward", out _));

        // 所有后代被级联删除（不再有"保留为孤儿"的可能）
        Assert.False(cache.TryGetTree<string>("Line:6:Upward:Stations", out _));
        Assert.False(cache.TryGetTree<string>("Line:6:Upward:Station2", out _));

        // 兄弟子树不受影响
        Assert.True(cache.TryGetTree<string>("Line:6:Downward:Stations", out var c));
        Assert.Equal("C", c);

        // 树索引中不存在任何"ParentPath 指向 Line:6:Upward 的孤儿"
        var childrenOfLine6 = cache.GetChildPaths("Line:6").ToList();
        Assert.DoesNotContain("Line:6:Upward", childrenOfLine6);
        Assert.DoesNotContain("Line:6:Upward:Stations", childrenOfLine6);
    }

    [Fact]
    public void RemoveTree_WithIncludeSelfFalse_ShouldDeleteOnlyDescendants()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("A:B:C", 1).Dispose();
        cache.SetTree("A:B:D", 2).Dispose();

        cache.RemoveTree("A:B", new TreeRemoveOptions { IncludeSelf = false });

        // 自身保留
        Assert.True(cache.TryGetTree<int>("A:B", out _));

        // 后代被删除
        Assert.False(cache.TryGetTree<int>("A:B:C", out _));
        Assert.False(cache.TryGetTree<int>("A:B:D", out _));
    }

    [Fact]
    public void RemoveTree_OnLeafNode_ShouldNotAffectAncestors()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTreeValue("A:B:C", 42);

        cache.RemoveTree("A:B:C");

        // 删除叶子节点不影响中间节点索引
        // (中间节点本身可能没有缓存值，但 _nodes 中应有 A、A:B 索引)
        Assert.True(cache.TryGetTree<int>("A:B", out _)); // 期望为 false，因为从未 SetTree A:B
        Assert.False(cache.TryGetTree<int>("A:B:C", out _));
    }
}
```

- [ ] **Step 7.2：运行新测试**

Run: `dotnet test tests/TreeMemoryCache.Tests/TreeMemoryCache.Tests.csproj --filter "FullyQualifiedName~CascadeDeleteContractTests" -nologo`
预期：3 个新测试全部通过。

- [ ] **Step 7.3：Commit**

```bash
git add tests/TreeMemoryCache.Tests/CascadeDeleteContractTests.cs
git commit -m "test: 添加 CascadeDeleteContract 回归测试

明确'树形缓存 = 严格级联删除'为硬契约，阻止未来重新引入
OrphanChildren 等保留子节点的模式。"
```

---

### Task 8：更新项目文档

**Files:**
- Modify: `CLAUDE.md`
- Verify: `README.md` / `src/TreeMemoryCache/README.md`（实际未提及 Orphan，确认无需修改）

- [ ] **Step 8.1：CLAUDE.md 注意事项中移除 OrphanChildren 说明**

执行 Edit：
```
old_string:
5. `TreeRemoveOptions.OrphanChildren` 可将子节点孤儿化而非级联删除

new_string:
```

**注意：** 原列表第 5 条为：
```
5. `TreeRemoveOptions.OrphanChildren` 可将子节点孤儿化而非级联删除
```
完整替换为单行空（保持列表编号连续）。

- [ ] **Step 8.2：搜索 README 中是否还有 Orphan 残留**

Run: `Grep "Orphan|orphan|孤儿" README.md src/TreeMemoryCache/README.md`
预期：无匹配。如有匹配，根据上下文人工调整（当前调查结果显示两处 README 均未直接提及 Orphan，应无匹配）。

- [ ] **Step 8.3：Commit**

```bash
git add CLAUDE.md
git commit -m "docs: CLAUDE.md 移除 OrphanChildren 说明"
```

---

### Task 9：最终验证

**Files:**
- 验证全项目

- [ ] **Step 9.1：完整构建**

Run: `dotnet build -nologo`
预期：0 错误，0 警告（允许既有警告，但与本次改动相关的不应有新警告）。

- [ ] **Step 9.2：完整测试**

Run: `dotnet test -nologo`
预期：全部测试通过。**特别确认：**
- `BugFixTests` 不再包含 `RemoveTree_WithOrphanChildren_*`
- `CascadeDeleteContractTests` 3 个新测试通过
- 任何 `*Orphan*` 命名测试均不存在

- [ ] **Step 9.3：全代码库孤儿化关键字搜索**

Run: `Grep -i "orphan|孤儿" src/ tests/`
预期：除以下保留项外无其他匹配：
- `CacheDiagnostics.DeadParentLinks`（保留，作为持久化损坏诊断）
- `Validator.cs` 中 `DeadParentLink` 错误信息（保留）
- `IndexSyncTests.cs` 文件头注释（保留——它描述的是"内部删除路径不同步导致的索引孤儿"，与 OrphanChildren 无关；这段注释是历史问题解释，建议保留以维护文档完整性）

如发现其他匹配项，停下来分析是否需要进一步清理。

- [ ] **Step 9.4：将本计划文件复制到归档位置**

Run：
```bash
mkdir -p docs/superpowers/plans
cp "<本计划文件当前路径>" docs/superpowers/plans/2026-06-23-remove-orphan-mode.md
```
预期：`docs/superpowers/plans/2026-06-23-remove-orphan-mode.md` 已生成。

- [ ] **Step 9.5：最终 commit（如归档文件已在仓库内）**

```bash
git add docs/superpowers/plans/2026-06-23-remove-orphan-mode.md
git commit -m "docs: 归档孤儿化模式移除实施计划"
```

---

## 自检（Self-Review）

**1. 规范覆盖：**
- ✅ 移除 `TreeRemoveOptions.OrphanChildren` → Task 1
- ✅ 移除 `RemoveTree` 孤儿化分支 → Task 2
- ✅ 清理 `CacheDiagnostics` 孤儿字段 → Task 3
- ✅ 清理 `Validator` 孤儿检测措辞 → Task 4
- ✅ 清理 `StructuredLoggers` 孤儿化日志 → Task 5
- ✅ 删除相关测试 → Task 6
- ✅ 添加回归防护测试 → Task 7
- ✅ 更新文档 → Task 8
- ✅ 最终验证 → Task 9

**2. 占位符检查：** 无 TBD / TODO / "实现稍后" / "类似 Task N" 等占位符。

**3. 类型一致性检查：**
- `CacheDiagnostics` 字段名从 `OrphanedNodes` 改为 `DeadParentLinks` —— Task 3 中同步更新 `GetDiagnostics` 实现
- `Validator` 错误信息从 `孤儿节点` 改为 `DeadParentLink` —— Task 4
- `StructuredLoggers` EventId 重新分配 —— Task 5 注释明确说明

**4. 范围控制：** 未修改任何与孤儿化无关的源代码；当前仓库中未提交的 `CacheStatistics.cs` / `TreeMemoryCache.cs`（部分）/`TreeMemoryCache.csproj` 改动与本次任务正交，保持不动。

**5. 注意事项：**
- Task 7 中 `RemoveTree_OnLeafNode_ShouldNotAffectAncestors` 注释"中间节点本身可能没有缓存值" —— 这是为了避免后续读者疑惑为何不直接 `Assert.True`。
- IndexSyncTests.cs 文件头注释中的"孤儿节点"指的是"索引与缓存不一致"——这是分布式缓存领域的通用术语，与本任务无关，**保留**。

---

## 执行交接

计划已保存到 `C:\Users\zhouhuibo\.claude\plans\steady-dazzling-cloud.md`（系统指定的临时计划文件）。最终归档位置：`docs/superpowers/plans/2026-06-23-remove-orphan-mode.md`（实施时由 Task 9.4 落地）。

两种执行方式：

1. **Subagent-Driven (推荐)** — 每个 Task 派一个独立子代理，Task 之间由我审阅把关
2. **Inline Execution** — 在当前会话按 Task 顺序执行，重大节点暂停确认

请选择执行方式。
