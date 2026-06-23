# TreeMemoryCache 项目指南

## 项目概述

这是一个基于 `Microsoft.Extensions.Caching.Memory` 的树形内存缓存扩展库，使用路径层级组织缓存 Key（如 `Line:6:Upward:Stations`），支持子树查询、批量操作、级联删除和持久化存储。

## 技术栈

- **.NET 8** (`net8.0`)
- **核心依赖**: `Microsoft.Extensions.Caching.Memory` 10.0.5
- **测试框架**: xUnit
- **诊断工具**: Spectre.Console（树形输出）

## 项目结构

```
TreeMemoryCache/
├── src/TreeMemoryCache/          # 核心库源码
│   ├── ITreeMemoryCache.cs      # 接口定义
│   ├── TreeMemoryCache.cs       # 核心实现（含 CacheStatisticsCollector 内部类）
│   ├── TreeCacheBatch.cs        # 批量操作
│   ├── CacheNode.cs             # 缓存节点数据结构（内部类）
│   ├── CacheStatistics.cs       # 统计信息模型（公开 DTO）
│   ├── TreeRemoveOptions.cs     # 删除选项
│   ├── TreeMemoryCacheExtensions.cs  # 扩展方法（SetTreeValue）
│   ├── ServiceCollectionExtensions.cs  # DI 扩展
│   ├── Persistence/             # 持久化模块
│   │   ├── ITreeCachePersistence.cs    # 持久化接口
│   │   ├── JsonFilePersistence.cs       # JSON 文件实现
│   │   ├── CacheNodeSnapshot.cs        # 节点快照
│   │   └── PersistenceStrategy.cs      # 策略枚举
│   ├── Diagnostics/              # 诊断模块
│   │   ├── CacheDiagnostics.cs          # 综合诊断信息
│   │   ├── ValidationResult.cs          # 验证结果
│   │   ├── OperationRecord.cs           # 操作记录
│   │   ├── Validator.cs                # 验证器（内部类）
│   │   └── DumpExtensions.cs           # 树形输出扩展
│   ├── Logging/                 # 日志模块
│   │   └── StructuredLoggers.cs         # 结构化日志（内部类）
│   └── Debugging/               # 调试模块
│       └── TreeMemoryCacheDebugView.cs  # 调试视图
├── samples/TreeMemoryCache.QuickStart/  # 快速开始示例
└── tests/TreeMemoryCache.Tests/         # 单元测试
```

## 核心概念

### 路径层级

- 使用 `:` 作为路径分隔符（如 `A:B:C`）
- 支持通配符匹配：`*` 匹配单段，`*:*` 匹配含分隔符的路径

### 树结构维护

- `CacheNode`（内部）存储路径、父子关系、标签、大小、版本
- `_nodes` 字典维护树形索引
- `_tagIndex` 维护标签到路径的倒排索引

### 线程安全

- 使用 `ReaderWriterLockSlim` 保护树结构
- 底层 `MemoryCache` 本身线程安全
- 原子操作（`Interlocked`）保护统计计数器

### 持久化

- 支持 JSON 文件持久化（`JsonFilePersistence`）
- 三种策略：`Synchronous`、`Asynchronous`、`Lazy`
- 提供 `SaveAsync()` / `LoadAsync()` 方法
- `SaveAsync()` 调用后自动清理脏标记

### 诊断与调试

- `CacheDiagnostics`：死指针数、内存估算、标签分布、深度路径
- `ValidationResult`：树结构一致性验证
- `OperationRecord`：操作历史追踪（最多 1000 条）
- `Dump()` 扩展：树形可视化输出

## API 速查

### 核心缓存操作

| 方法 | 说明 |
|------|------|
| `SetTree(path, value)` | 设置缓存项 |
| `SetTree(path, value, tag)` | 设置缓存项并指定标签 |
| `TryGetTree<T>(path, out value)` | 获取缓存项 |
| `RemoveTree(path)` | 删除子树（默认含自身） |
| `RemoveTree(path, options)` | 删除子树（可配置 IncludeSelf） |
| `RemoveTreeAsync(path)` | 异步删除子树，返回每个被删路径 |
| `RemoveByTag(tag)` | 按标签删除所有路径 |
| `CreateBatch()` | 创建批量操作 |

### 查询操作

| 方法 | 说明 |
|------|------|
| `GetChildPaths(path)` | 获取直接子路径 |
| `GetDescendantPaths(path)` | 获取全部后代路径 |
| `GetPathsByPattern(pattern)` | 按通配符查询 |
| `GetPathsByTag(tag)` | 按标签查询 |

### 统计与诊断

| 方法 | 说明 |
|------|------|
| `GetStatistics()` | 获取缓存统计（节点数、大小、命中率等） |
| `GetDiagnostics()` | 获取综合诊断信息 |
| `Validate()` | 验证树结构一致性 |
| `GetOperationHistory()` | 获取操作历史记录 |

### 持久化

| 方法 | 说明 |
|------|------|
| `SaveAsync()` | 保存到持久化存储 |
| `LoadAsync()` | 从持久化存储加载 |
| `Persistence.SaveAsync()` | 直接调用持久化器保存 |
| `Persistence.GetMetadataAsync()` | 获取存储元数据 |

### 便捷扩展

| 方法 | 说明 |
|------|------|
| `SetTreeValue(path, value)` | 写入缓存并自动提交 |
| `SetTreeValue(path, value, tag)` | 写入缓存带标签并自动提交 |

## 常用命令

```bash
# 构建
dotnet build

# 运行测试
dotnet test

# 打包 NuGet
dotnet pack

# 运行示例
dotnet run --project samples/TreeMemoryCache.QuickStart
```

## 开发约定

### 代码风格

- 使用中文注释（XML Doc）
- 文件编码：UTF-8 with BOM
- 行尾：LF

### 命名规范

- **公开类/接口**：PascalCase（如 `ITreeMemoryCache`）
- **内部类**：PascalCase，前面加 `_` 表示私有字段
- **命名空间**：`TreeMemoryCache`、`TreeMemoryCache.Persistence`、`TreeMemoryCache.Diagnostics`

### 分支命名

- `feat/` - 新功能
- `fix/` - 修复
- `refactor/` - 重构
- `docs/` - 文档

### 提交规范

- `feat:` - 新功能
- `fix:` - 修复
- `docs:` - 文档
- `refactor:` - 重构

## 注意事项

1. 路径会自动规范化（Trim 两端空格和 `:`）
2. 删除父节点时，可选是否删除自身（`IncludeSelf` 选项）
3. 标签只设置在目标路径节点，中间路径节点标签始终为 null
4. 批量操作在单个写锁内执行
5. 持久化需在构造函数传入 `ITreeCachePersistence` 实例
6. 操作历史记录最多保留 1000 条，超出后自动清理旧记录
8. `CacheStatisticsCollector` 是内部统计收集器，`CacheStatistics` 是公开的数据模型
