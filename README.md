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

- .NET 9（`net9.0`）

## 快速开始

### 直接使用

```csharp
using TreeMemoryCache;

using var cache = new TreeMemoryCache.TreeMemoryCache();

using var entry = cache.SetTree("Line:6:Upward:Stations", new[] { "A", "B", "C" });

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

## 示例与测试

- 示例项目：`samples/TreeMemoryCache.QuickStart`
- 测试项目：`tests/TreeMemoryCache.Tests`

## 文档

更完整的设计说明、流程图、API 说明见：`src/TreeMemoryCache/README.md`

## License

MIT
