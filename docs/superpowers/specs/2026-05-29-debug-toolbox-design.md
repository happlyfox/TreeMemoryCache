# TreeMemoryCache 调试工具箱设计方案

## 概述

为 TreeMemoryCache 添加完整的调试工具箱，提升开发者体验，便于诊断和排查问题。

## 目标

1. **IDE 可视化** — 在调试器中直接查看缓存树结构
2. **诊断方法** — 提供丰富的诊断 API
3. **日志增强** — 结构化日志 + 操作追踪

## 实现方案

### 1. IDE 可视化

#### 1.1 DebuggerDisplay 属性

为 `CacheNode` 和关键类添加 `DebuggerDisplay`：

```csharp
[DebuggerDisplay("{Path} | Tag: {Tag ?? \"-\"} | Children: {ChildPaths.Count}")]
internal sealed class CacheNode
{
    // ...
}
```

效果：调试时 Watch 窗口显示 `Line:6:Upward | Tag: - | Children: 2`

#### 1.2 DebuggerTypeProxy

为 `TreeMemoryCache` 实现 `DebuggerTypeProxy`，提供树形结构视图：

```csharp
[DebuggerTypeProxy(typeof(TreeMemoryCacheDebugView))]
public sealed class TreeMemoryCache : ITreeMemoryCache
{
    private class TreeMemoryCacheDebugView
    {
        public IEnumerable<CacheNode> RootNodes { get; }
        public int TotalNodeCount { get; }
        public CacheStatistics Statistics { get; }
    }
}
```

效果：展开 TreeMemoryCache 时，显示根节点列表和统计信息

### 2. 诊断方法

#### 2.1 Dump() — 树形文本输出

使用 Spectre.Console 输出彩色树形结构：

```csharp
public void Dump(TextWriter? writer = null)
// 输出示例：
// 📁 TreeMemoryCache (节点数: 5)
// ├── Line:6
// │   ├── Upward
// │   │   ├── Stations [tag: temp]
// │   │   └── Station2
// │   └── Downward
// └── Line:8
```

#### 2.2 GetDiagnostics() — 综合诊断信息

```csharp
public CacheDiagnostics GetDiagnostics()
// 返回：
public sealed class CacheDiagnostics
{
    public int TotalNodes { get; }
    public int OrphanedNodes { get; }      // 孤儿节点数量
    public int DeadParentLinks { get; }    // 指向不存在父节点的子节点
    public long EstimatedMemoryBytes { get; }
    public IReadOnlyDictionary<string, int> TagDistribution { get; }
    public IReadOnlyList<string> DeepestPaths { get; }  // 最深路径 TOP 5
    public IReadOnlyList<string> OrphanedPaths { get; } // 孤儿节点列表
}
```

#### 2.3 Validate() — 一致性检查

```csharp
public ValidationResult Validate()
// 返回：
public sealed class ValidationResult
{
    public bool IsValid { get; }
    public IReadOnlyList<string> Errors { get; }
    public IReadOnlyList<string> Warnings { get; }
}
```

检查项：
- 孤儿节点（ParentPath 指向不存在的节点）
- 标签索引一致性（_tagIndex 与节点 Tag 是否匹配）
- 子节点引用完整性（ChildPaths 中的路径是否都在 _nodes 中）

### 3. 日志增强

#### 3.1 结构化日志

使用 `LoggerMessage.Define` 避免字符串拼接：

```csharp
private static readonly Action<ILogger, string, int, long, Exception?>
    LogCascadeDelete = LoggerMessage.Define<string, int, long>(
        LogLevel.Information,
        0, "级联删除完成: {Path}, 删除 {Count} 个节点, 耗时 {DurationMs}ms");
```

#### 3.2 操作追踪

添加可选的操作追踪功能：

```csharp
// 在 appsettings.json 中配置
{
  "TreeMemoryCache": {
    "EnableOperationTracking": true,
    "MaxTrackedOperations": 1000
  }
}

// 追踪记录
public sealed class OperationRecord
{
    public DateTimeOffset Timestamp { get; }
    public OperationType Type { get; }
    public string Path { get; }
    public string? CallerInfo { get; }  // 方法名、文件、行号
}

// 获取追踪记录
public IReadOnlyList<OperationRecord> GetOperationHistory()
```

### 4. 新增文件

```
src/TreeMemoryCache/
├── Diagnostics/
│   ├── CacheDiagnostics.cs        # 诊断信息模型
│   ├── ValidationResult.cs        # 验证结果模型
│   ├── OperationRecord.cs        # 操作记录模型
│   ├── DumpExtensions.cs          # Dump() 扩展方法
│   └── Validator.cs               # 一致性验证逻辑
├── Debugging/
│   └── TreeMemoryCacheDebugView.cs # DebuggerTypeProxy
└── Logging/
    └── StructuredLoggers.cs        # 结构化日志定义
```

### 5. 依赖

```xml
<PackageReference Include="Spectre.Console" Version="0.49.0" />
```

## 测试策略

1. **单元测试**：Validator 的一致性检查逻辑
2. **快照测试**：Dump() 输出的树形结构
3. **集成测试**：DebuggerTypeProxy 在调试器中的表现

## 兼容性

- 所有新增 API 为 extension methods 或新类，不影响现有接口
- `Dump()` 等方法为可选功能，使用条件编译避免强制依赖

## 待确认

- [x] 诊断输出格式：使用 Spectre.Console
- [x] 功能范围：IDE 可视化 + 诊断方法 + 日志增强
