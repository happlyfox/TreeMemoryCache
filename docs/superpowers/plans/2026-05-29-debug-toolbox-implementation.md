# TreeMemoryCache 调试工具箱实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 TreeMemoryCache 添加完整的调试工具箱，包括 IDE 可视化、诊断方法、日志增强

**Architecture:** 通过新增诊断类、扩展方法和调试视图实现，零破坏性修改现有 API

**Tech Stack:** .NET 9, Spectre.Console, Microsoft.Extensions.Logging

---

## 文件结构

```
src/TreeMemoryCache/
├── Diagnostics/
│   ├── CacheDiagnostics.cs      # 诊断信息模型
│   ├── ValidationResult.cs      # 验证结果模型
│   ├── OperationRecord.cs       # 操作记录模型
│   ├── DumpExtensions.cs        # Dump() 扩展方法
│   └── Validator.cs             # 一致性验证逻辑
├── Debugging/
│   └── TreeMemoryCacheDebugView.cs  # DebuggerTypeProxy
└── Logging/
    └── StructuredLoggers.cs      # 结构化日志定义
```

---

## Task 1: 添加 Spectre.Console 依赖并创建目录结构

**Files:**
- Modify: `src/TreeMemoryCache/TreeMemoryCache.csproj`
- Create: `src/TreeMemoryCache/Diagnostics/` (目录)
- Create: `src/TreeMemoryCache/Debugging/` (目录)
- Create: `src/TreeMemoryCache/Logging/` (目录)

- [ ] **Step 1: 添加 Spectre.Console NuGet 包引用**

```xml
<PackageReference Include="Spectre.Console" Version="0.49.0" />
```

- [ ] **Step 2: 提交**

```bash
git add src/TreeMemoryCache/TreeMemoryCache.csproj
git commit -m "chore: 添加 Spectre.Console 依赖"
```

---

## Task 2: 实现 CacheDiagnostics 诊断信息模型

**Files:**
- Create: `src/TreeMemoryCache/Diagnostics/CacheDiagnostics.cs`
- Create: `tests/TreeMemoryCache.Tests/DiagnosticsTests.cs`

- [ ] **Step 1: 编写失败的测试**

```csharp
[Fact]
public void GetDiagnostics_ShouldReturnCorrectNodeCount()
{
    using var cache = new TreeMemoryCache();
    cache.SetTree("A:B:C", "value").Dispose();
    cache.SetTree("A:D", "value2").Dispose();

    var diagnostics = cache.GetDiagnostics();

    Assert.Equal(5, diagnostics.TotalNodes); // A, A:B, A:B:C, A:D
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test --filter "GetDiagnostics_ShouldReturnCorrectNodeCount"`
Expected: FAIL with "CacheDiagnostics not found"

- [ ] **Step 3: 实现 CacheDiagnostics**

```csharp
namespace TreeMemoryCache.Diagnostics;

public sealed class CacheDiagnostics
{
    public int TotalNodes { get; init; }
    public int OrphanedNodes { get; init; }
    public int DeadParentLinks { get; init; }
    public long EstimatedMemoryBytes { get; init; }
    public Dictionary<string, int> TagDistribution { get; init; } = new();
    public List<string> DeepestPaths { get; init; } = new();
    public List<string> OrphanedPaths { get; init; } = new();
}
```

- [ ] **Step 4: 运行测试验证通过**

Run: `dotnet test --filter "GetDiagnostics_ShouldReturnCorrectNodeCount"`
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/TreeMemoryCache/Diagnostics/CacheDiagnostics.cs tests/TreeMemoryCache.Tests/DiagnosticsTests.cs
git commit -m "feat: 添加 CacheDiagnostics 诊断信息模型"
```

---

## Task 3: 实现 ValidationResult 验证结果模型

**Files:**
- Create: `src/TreeMemoryCache/Diagnostics/ValidationResult.cs`
- Modify: `tests/TreeMemoryCache.Tests/DiagnosticsTests.cs`

- [ ] **Step 1: 编写失败的测试**

```csharp
[Fact]
public void Validate_OnValidCache_ShouldReturnNoErrors()
{
    using var cache = new TreeMemoryCache();
    cache.SetTree("A:B:C", "value").Dispose();

    var result = cache.Validate();

    Assert.True(result.IsValid);
    Assert.Empty(result.Errors);
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test --filter "Validate_OnValidCache_ShouldReturnNoErrors"`
Expected: FAIL with "Validate method not found"

- [ ] **Step 3: 实现 ValidationResult**

```csharp
namespace TreeMemoryCache.Diagnostics;

public sealed class ValidationResult
{
    public bool IsValid { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}
```

- [ ] **Step 4: 运行测试验证通过**

Run: `dotnet test --filter "Validate_OnValidCache_ShouldReturnNoErrors"`
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/TreeMemoryCache/Diagnostics/ValidationResult.cs tests/TreeMemoryCache.Tests/DiagnosticsTests.cs
git commit -m "feat: 添加 ValidationResult 验证结果模型"
```

---

## Task 4: 实现 Validator 一致性验证逻辑

**Files:**
- Create: `src/TreeMemoryCache/Diagnostics/Validator.cs`
- Modify: `src/TreeMemoryCache/TreeMemoryCache.cs` (添加 Validate 方法)
- Modify: `tests/TreeMemoryCache.Tests/DiagnosticsTests.cs`

- [ ] **Step 1: 编写失败的测试**

```csharp
[Fact]
public void Validate_OnOrphanedNode_ShouldReportError()
{
    using var cache = new TreeMemoryCache();
    cache.SetTree("A:B:C", "value").Dispose();
    // 模拟孤儿节点：通过反射设置 ParentPath
    var node = GetPrivateNode(cache, "A:B:C");
    node.ParentPath = "NonExistent";

    var result = cache.Validate();

    Assert.False(result.IsValid);
    Assert.Contains(result.Errors, e => e.Contains("Orphaned"));
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test --filter "Validate_OnOrphanedNode_ShouldReportError"`
Expected: FAIL with "Validate returns empty errors"

- [ ] **Step 3: 实现 Validator**

```csharp
namespace TreeMemoryCache.Diagnostics;

internal static class Validator
{
    public static ValidationResult Validate(
        ConcurrentDictionary<string, CacheNode> nodes,
        ConcurrentDictionary<string, HashSet<string>> tagIndex)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // 检查孤儿节点
        foreach (var (path, node) in nodes)
        {
            if (node.ParentPath is not null && !nodes.ContainsKey(node.ParentPath))
            {
                errors.Add($"孤儿节点: {path} 指向不存在的父节点 {node.ParentPath}");
            }
        }

        // 检查标签索引一致性
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

- [ ] **Step 4: 在 TreeMemoryCache 添加 Validate 扩展方法**

```csharp
public ValidationResult Validate()
{
    EnsureNotDisposed();
    return Validator.Validate(_nodes, _tagIndex);
}
```

- [ ] **Step 5: 运行测试验证通过**

Run: `dotnet test --filter "Validate"`
Expected: PASS

- [ ] **Step 6: 提交**

```bash
git add src/TreeMemoryCache/Diagnostics/Validator.cs src/TreeMemoryCache/TreeMemoryCache.cs tests/TreeMemoryCache.Tests/DiagnosticsTests.cs
git commit -m "feat: 添加 Validator 一致性验证逻辑"
```

---

## Task 5: 实现 DumpExtensions 树形文本输出

**Files:**
- Create: `src/TreeMemoryCache/Diagnostics/DumpExtensions.cs`
- Modify: `src/TreeMemoryCache/TreeMemoryCache.cs` (添加 Dump 方法)
- Modify: `tests/TreeMemoryCache.Tests/DiagnosticsTests.cs`

- [ ] **Step 1: 编写失败的测试**

```csharp
[Fact]
public void Dump_ShouldOutputTreeStructure()
{
    using var cache = new TreeMemoryCache();
    cache.SetTree("A:B:C", "value").Dispose();
    cache.SetTree("A:D", "value2").Dispose();

    using var writer = new StringWriter();
    cache.Dump(writer);
    var output = writer.ToString();

    Assert.Contains("A", output);
    Assert.Contains("B", output);
    Assert.Contains("C", output);
    Assert.Contains("D", output);
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test --filter "Dump_ShouldOutputTreeStructure"`
Expected: FAIL with "Dump method not found"

- [ ] **Step 3: 实现 DumpExtensions**

```csharp
using Spectre.Console;
using Spectre.Console.Rendering;

namespace TreeMemoryCache.Diagnostics;

public static class DumpExtensions
{
    public static void Dump(this TreeMemoryCache cache, TextWriter? writer = null)
    {
        writer ??= Console.Out;
        AnsiConsole.Render(BuildTree(cache));
        writer.WriteLine();
    }

    private static Tree BuildTree(TreeMemoryCache cache)
    {
        var rootNodes = cache.GetRootNodes();
        var tree = new Tree("[bold]TreeMemoryCache[/]");

        foreach (var (path, _) in rootNodes)
        {
            BuildTreeNode(tree, path, cache);
        }

        return tree;
    }

    private static void BuildTreeNode(Tree tree, string path, TreeMemoryCache cache)
    {
        if (cache.TryGetTree<object>(path, out _))
        {
            cache.TryGetTree<object>(path, out var value);
            var node = tree.AddNode($"[green]{path.EscapeMarkup()}[/]");
        }
        else
        {
            var node = tree.AddNode($"[dim]{path.EscapeMarkup()}[/]");
        }

        foreach (var childPath in cache.GetChildPaths(path))
        {
            BuildTreeNode(node, childPath, cache);
        }
    }
}
```

- [ ] **Step 4: 在 TreeMemoryCache 添加 GetRootNodes 和 Dump 方法**

```csharp
public IEnumerable<KeyValuePair<string, CacheNode>> GetRootNodes()
{
    EnsureNotDisposed();
    _structureLock.EnterReadLock();
    try
    {
        return _nodes.Where(kv => kv.Value.ParentPath is null).ToList();
    }
    finally
    {
        _structureLock.ExitReadLock();
    }
}

public void Dump(TextWriter? writer = null)
{
    this.Dump(writer);
}
```

- [ ] **Step 5: 运行测试验证通过**

Run: `dotnet test --filter "Dump_ShouldOutputTreeStructure"`
Expected: PASS

- [ ] **Step 6: 提交**

```bash
git add src/TreeMemoryCache/Diagnostics/DumpExtensions.cs src/TreeMemoryCache/TreeMemoryCache.cs tests/TreeMemoryCache.Tests/DiagnosticsTests.cs
git commit -m "feat: 添加 Dump 树形文本输出功能"
```

---

## Task 6: 实现 GetDiagnostics 综合诊断方法

**Files:**
- Modify: `src/TreeMemoryCache/TreeMemoryCache.cs` (添加 GetDiagnostics 方法)
- Modify: `tests/TreeMemoryCache.Tests/DiagnosticsTests.cs`

- [ ] **Step 1: 编写失败的测试**

```csharp
[Fact]
public void GetDiagnostics_ShouldReturnOrphanedNodes()
{
    using var cache = new TreeMemoryCache();
    cache.SetTree("A:B:C", "value").Dispose();

    var diagnostics = cache.GetDiagnostics();

    Assert.NotNull(diagnostics.TagDistribution);
    Assert.NotNull(diagnostics.OrphanedPaths);
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test --filter "GetDiagnostics_ShouldReturnOrphanedNodes"`
Expected: FAIL

- [ ] **Step 3: 实现 GetDiagnostics**

```csharp
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
            // 检查孤儿节点
            if (node.ParentPath is not null && !_nodes.ContainsKey(node.ParentPath))
            {
                orphanedPaths.Add(path);
            }

            // 标签分布
            if (node.Tag is not null)
            {
                tagDistribution.TryGetValue(node.Tag, out var count);
                tagDistribution[node.Tag] = count + 1;
            }
        }

        // 最深路径 TOP 5
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
```

- [ ] **Step 4: 运行测试验证通过**

Run: `dotnet test --filter "GetDiagnostics"`
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/TreeMemoryCache/TreeMemoryCache.cs tests/TreeMemoryCache.Tests/DiagnosticsTests.cs
git commit -m "feat: 添加 GetDiagnostics 综合诊断方法"
```

---

## Task 7: 实现 DebuggerTypeProxy IDE 可视化

**Files:**
- Create: `src/TreeMemoryCache/Debugging/TreeMemoryCacheDebugView.cs`
- Modify: `src/TreeMemoryCache/TreeMemoryCache.cs` (添加 DebuggerTypeProxy)
- Modify: `src/TreeMemoryCache/CacheNode.cs` (添加 DebuggerDisplay)

- [ ] **Step 1: 编写失败的测试（IDE 功能难以单元测试，验证属性存在）**

```csharp
[Fact]
public void CacheNode_ShouldHaveDebuggerDisplay()
{
    var type = typeof(CacheNode);
    var attrs = type.GetCustomAttributes(typeof(DebuggerDisplayAttribute), false);

    Assert.NotEmpty(attrs);
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test --filter "CacheNode_ShouldHaveDebuggerDisplay"`
Expected: FAIL

- [ ] **Step 3: 为 CacheNode 添加 DebuggerDisplay**

```csharp
[DebuggerDisplay("{Path} | Tag: {Tag ?? \"-\"} | Children: {ChildPaths.Count}")]
internal sealed class CacheNode
{
    // ...
}
```

- [ ] **Step 4: 实现 TreeMemoryCacheDebugView**

```csharp
using System.Diagnostics;

namespace TreeMemoryCache.Debugging;

[DebuggerDisplay("{DebuggerDisplay,nq}", Name = "{DebuggerDisplay,nq}")]
internal sealed class TreeMemoryCacheDebugView
{
    private readonly TreeMemoryCache _cache;

    public TreeMemoryCacheDebugView(TreeMemoryCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public CacheNodeDebugView[] Nodes => _cache.GetNodesForDebug()
        .Select(kv => new CacheNodeDebugView(kv.Value))
        .ToArray();

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => $"TreeMemoryCache (节点: {_cache.GetStatistics().TotalNodeCount})";
}

[DebuggerDisplay("{Path} | {Tag ?? \"-\"}")]
internal sealed class CacheNodeDebugView
{
    private readonly CacheNode _node;

    public CacheNodeDebugView(CacheNode node) => _node = node;

    public string Path => _node.Path;
    public string? Tag => _node.Tag;
    public int ChildCount => _node.ChildPaths.Count;
}
```

- [ ] **Step 5: 运行测试验证通过**

Run: `dotnet test --filter "CacheNode_ShouldHaveDebuggerDisplay"`
Expected: PASS

- [ ] **Step 6: 在 TreeMemoryCache 添加 GetNodesForDebug 方法**

```csharp
[DebuggerTypeProxy(typeof(TreeMemoryCacheDebugView))]
public sealed class TreeMemoryCache : ITreeMemoryCache
{
    // ...
    internal IEnumerable<KeyValuePair<string, CacheNode>> GetNodesForDebug()
    {
        return _nodes;
    }
}
```

- [ ] **Step 7: 提交**

```bash
git add src/TreeMemoryCache/Debugging/TreeMemoryCacheDebugView.cs src/TreeMemoryCache/CacheNode.cs src/TreeMemoryCache/TreeMemoryCache.cs tests/TreeMemoryCache.Tests/DiagnosticsTests.cs
git commit -m "feat: 添加 DebuggerTypeProxy IDE 可视化支持"
```

---

## Task 8: 实现结构化日志

**Files:**
- Create: `src/TreeMemoryCache/Logging/StructuredLoggers.cs`
- Modify: `src/TreeMemoryCache/TreeMemoryCache.cs` (使用结构化日志)

- [ ] **Step 1: 创建 StructuredLoggers**

```csharp
namespace TreeMemoryCache.Logging;

using Microsoft.Extensions.Logging;

internal static class StructuredLoggers
{
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

    private static readonly Action<ILogger, string, Exception?> LogCacheHit =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            2,
            "缓存命中: {Path}");

    private static readonly Action<ILogger, string, Exception?> LogCacheMiss =
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
        LogCacheHit(logger, path, null);
    }

    public static void LogCacheMiss(ILogger? logger, string path)
    {
        LogCacheMiss(logger, path, null);
    }
}
```

- [ ] **Step 2: 修改 TreeMemoryCache 使用结构化日志**

将现有的字符串插值日志替换为 StructuredLoggers 方法调用

- [ ] **Step 3: 提交**

```bash
git add src/TreeMemoryCache/Logging/StructuredLoggers.cs src/TreeMemoryCache/TreeMemoryCache.cs
git commit -m "refactor: 使用结构化日志替代字符串插值"
```

---

## Task 9: 最终验证

**Files:**
- Modify: `tests/TreeMemoryCache.Tests/DiagnosticsTests.cs`

- [ ] **Step 1: 运行完整测试套件**

Run: `dotnet test`
Expected: 全部通过

- [ ] **Step 2: 运行诊断功能测试**

Run: `dotnet test --filter "DiagnosticsTests"`
Expected: 全部通过

- [ ] **Step 3: 提交**

```bash
git add -A
git commit -m "chore: 完成调试工具箱功能开发"
```

---

## Task 10: 实现操作追踪功能

**Files:**
- Create: `src/TreeMemoryCache/Diagnostics/OperationRecord.cs`
- Modify: `src/TreeMemoryCache/TreeMemoryCache.cs` (添加追踪逻辑)
- Create: `tests/TreeMemoryCache.Tests/DiagnosticsTests.cs`

- [ ] **Step 1: 编写失败的测试**

```csharp
[Fact]
public void GetOperationHistory_ShouldTrackSetOperation()
{
    using var cache = new TreeMemoryCache();
    cache.SetTree("A:B", "value").Dispose();

    var history = cache.GetOperationHistory();

    Assert.NotEmpty(history);
    Assert.Contains(history, r => r.Path == "A:B" && r.Type == OperationType.Set);
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test --filter "GetOperationHistory_ShouldTrackSetOperation"`
Expected: FAIL

- [ ] **Step 3: 实现 OperationRecord**

```csharp
namespace TreeMemoryCache.Diagnostics;

public sealed class OperationRecord
{
    public DateTimeOffset Timestamp { get; init; }
    public OperationType Type { get; init; }
    public string Path { get; init; } = string.Empty;
    public string? Tag { get; init; }
    public string? CallerMemberName { get; init; }
    public string? CallerFilePath { get; init; }
    public int CallerLineNumber { get; init; }
}
```

- [ ] **Step 4: 在 TreeMemoryCache 添加追踪字段和方法**

```csharp
private readonly ConcurrentQueue<OperationRecord> _operationHistory;
private const int MaxTrackedOperations = 1000;

public IReadOnlyList<OperationRecord> GetOperationHistory()
{
    return _operationHistory.ToArray().Reverse().Take(100).Reverse().ToList();
}

private void TrackOperation(OperationType type, string path, string? tag = null,
    [CallerMemberName] string? memberName = null,
    [CallerFilePath] string? filePath = null,
    [CallerLineNumber] int lineNumber = 0)
{
    var record = new OperationRecord
    {
        Timestamp = DateTimeOffset.UtcNow,
        Type = type,
        Path = path,
        Tag = tag,
        CallerMemberName = memberName,
        CallerFilePath = filePath,
        CallerLineNumber = lineNumber
    };

    _operationHistory.Enqueue(record);
    while (_operationHistory.Count > MaxTrackedOperations)
    {
        _operationHistory.TryDequeue(out _);
    }
}
```

- [ ] **Step 5: 在 SetTree/RemoveTree 中调用 TrackOperation**

- [ ] **Step 6: 运行测试验证通过**

Run: `dotnet test --filter "GetOperationHistory"`
Expected: PASS

- [ ] **Step 7: 提交**

```bash
git add src/TreeMemoryCache/Diagnostics/OperationRecord.cs src/TreeMemoryCache/TreeMemoryCache.cs tests/TreeMemoryCache.Tests/DiagnosticsTests.cs
git commit -m "feat: 添加操作追踪功能"
```

---

## 实现检查清单

- [ ] Task 1: Spectre.Console 依赖
- [ ] Task 2: CacheDiagnostics 模型
- [ ] Task 3: ValidationResult 模型
- [ ] Task 4: Validator 验证逻辑
- [ ] Task 5: Dump 树形输出
- [ ] Task 6: GetDiagnostics 综合诊断
- [ ] Task 7: DebuggerTypeProxy IDE 可视化
- [ ] Task 8: 结构化日志
- [ ] Task 9: 最终验证
- [ ] Task 10: 操作追踪功能
