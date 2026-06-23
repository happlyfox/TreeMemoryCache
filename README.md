# TreeMemoryCache

基于 `Microsoft.Extensions.Caching.Memory` 的树形内存缓存扩展：用路径层级组织缓存 Key，支持子树查询、批量操作与级联删除。

## 特性

- 树形路径缓存：使用 `:` 作为层级分隔符，例如 `Line:6:Upward:Stations`
- 级联删除：删除父路径时可一并删除所有后代路径
- 层级查询：获取直接子节点或全部后代节点路径
- 路径模式匹配：按路径段支持 `*` 通配符匹配
- 批量操作：通过 `TreeCacheBatch` 一次提交多条变更
- 依赖注入友好：提供 `IServiceCollection` 扩展快速接入
- 统计与观测：命中/未命中、级联删除次数、平均访问耗时等

## 环境要求

- .NET 8（`net8.0`），可运行在 .NET 6/7/8/9 环境中

## 快速开始

### 直接使用

```csharp
using TreeMemoryCache;

using var cache = new TreeMemoryCache.TreeMemoryCache();

// 写入（便捷扩展，自动 Dispose）
cache.SetTreeValue("Line:6:Upward:Stations", new[] { "A", "B", "C" });

// 带标签写入
cache.SetTreeValue("Line:6:Upward:Timetable", timetable, tag: "Line:6");

// 读取
if (cache.TryGetTree<string[]>("Line:6:Upward:Stations", out var stations))
{
    Console.WriteLine(string.Join(",", stations!));
}
```

### 依赖注入（DI）

```csharp
using Microsoft.Extensions.DependencyInjection;
using TreeMemoryCache;

var services = new ServiceCollection();

services.AddTreeMemoryCache(options =>
{
    options.SizeLimit = 10_000;
});

var provider = services.BuildServiceProvider();
var cache = provider.GetRequiredService<ITreeMemoryCache>();
```

## 基本用法

### 单点删除

```csharp
// 删除单个路径（不影响其他节点）
cache.RemoveTree("Line:6:Upward:Stations");
```

### 子树级联删除

默认情况下 `RemoveTree` 会级联删除该路径下的所有后代节点。

```csharp
// 级联删除 "Line:6" 下的所有节点
cache.RemoveTree("Line:6");

// 只删除自身，保留后代
cache.RemoveTree("Line:6", new TreeRemoveOptions
{
    IncludeSelf = true,
    OrphanChildren = true   // 断开父子关系但保留子节点
});

// 删除后代但保留自身
cache.RemoveTree("Line:6", new TreeRemoveOptions
{
    IncludeSelf = false
});

// 带进度回调的删除
cache.RemoveTree("Line:6", new TreeRemoveOptions
{
    OnProgress = count => Console.WriteLine($"已删除 {count} 个节点")
});
```

### 异步子树删除

在长路径删除场景下，可以使用异步流并支持取消：

```csharp
await foreach (var removedPath in cache.RemoveTreeAsync("Line:6", cancellationToken))
{
    Console.WriteLine($"已删除: {removedPath}");
}
```

### 标签删除

通过标签可以将多个无层级关系的缓存项归类，删除时一键清理。

```csharp
// 写入时打标签
cache.SetTreeValue("User:1001:Profile", profile, tag: "user:1001");
cache.SetTreeValue("User:1001:Orders", orders,   tag: "user:1001");
cache.SetTreeValue("User:1001:Cart",   cart,     tag: "user:1001");

// 查询该标签下的所有路径
foreach (var path in cache.GetPathsByTag("user:1001"))
{
    Console.WriteLine(path);
}

// 一键删除该标签关联的所有节点
cache.RemoveByTag("user:1001");
```

### 批量操作

当需要原子地提交多条 Set/Remove 操作时，使用 `CreateBatch()` 一次写入，避免多次加锁。

```csharp
using (var batch = cache.CreateBatch())
{
    batch.Set("Line:1:Stations",   line1Stations);
    batch.Set("Line:2:Stations",   line2Stations, tag: "metro");
    batch.Set("Line:3:Stations",   line3Stations, tag: "metro");
    batch.Remove("Line:99:Stations");
    batch.RemoveTree("Deprecated:Path");

    batch.Execute();   // 提交前不会真正生效
}
```

> `TreeCacheBatch` 必须显式调用 `Execute()`，否则在 Dispose 时会抛出 `InvalidOperationException`，防止误操作被静默丢弃。

### 路径模式匹配

支持 `*` 通配符匹配单段路径：

```csharp
// 匹配第一段为 "Line" 的所有路径
foreach (var path in cache.GetPathsByPattern("Line:*"))
{
    Console.WriteLine(path);
}

// 匹配 "Line:6" 下所有后代
foreach (var path in cache.GetPathsByPattern("Line:6:*"))
{
    Console.WriteLine(path);
}
```

### 层级查询

```csharp
// 获取直接子路径
IEnumerable<string> children = cache.GetChildPaths("Line:6");

// 获取所有后代路径（广度优先）
IEnumerable<string> descendants = cache.GetDescendantPaths("Line:6");
```

### 缓存统计

```csharp
var stats = cache.GetStatistics();
Console.WriteLine($"条目数: {stats.EntryCount}");
Console.WriteLine($"命中率: {stats.HitRate:P2}");
Console.WriteLine($"级联删除次数: {stats.CascadeRemoveCount}");
```

## 示例与测试

- 示例项目：`samples/TreeMemoryCache.QuickStart`
- 测试项目：`tests/TreeMemoryCache.Tests`

## 文档

更完整的设计说明、流程图、API 说明见：`src/TreeMemoryCache/README.md`

## License

MIT
