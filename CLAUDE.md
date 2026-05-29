# TreeMemoryCache 项目指南

## 项目概述

这是一个基于 `Microsoft.Extensions.Caching.Memory` 的树形内存缓存扩展库，使用路径层级组织缓存 Key（如 `Line:6:Upward:Stations`），支持子树查询、批量操作和级联删除。

## 技术栈

- **.NET 9** (`net9.0`)
- **核心依赖**: `Microsoft.Extensions.Caching.Memory`
- **测试框架**: xUnit

## 项目结构

```
TreeMemoryCache/
├── src/TreeMemoryCache/          # 核心库源码
│   ├── ITreeMemoryCache.cs      # 接口定义
│   ├── TreeMemoryCache.cs       # 核心实现
│   ├── TreeCacheBatch.cs        # 批量操作
│   ├── CacheNode.cs             # 缓存节点数据结构
│   ├── CacheStatistics.cs       # 统计信息模型
│   ├── TreeRemoveOptions.cs     # 删除选项
│   └── ServiceCollectionExtensions.cs  # DI 扩展
├── samples/TreeMemoryCache.QuickStart/  # 快速开始示例
└── tests/TreeMemoryCache.Tests/         # 单元测试
```

## 核心概念

### 路径层级

- 使用 `:` 作为路径分隔符（如 `A:B:C`）
- 支持通配符匹配：`*` 匹配单段，`*:*` 匹配含分隔符的路径

### 树结构维护

- `CacheNode` 存储路径、父子关系、标签
- `_nodes` 字典维护树形索引
- `_tagIndex` 维护标签到路径的倒排索引

### 线程安全

- 使用 `ReaderWriterLockSlim` 保护树结构
- 底层 `MemoryCache` 本身线程安全

## API 速查

| 方法 | 说明 |
|------|------|
| `SetTree(path, value)` | 设置缓存项 |
| `TryGetTree<T>(path, out value)` | 获取缓存项 |
| `RemoveTree(path)` | 删除子树 |
| `GetChildPaths(path)` | 获取直接子路径 |
| `GetDescendantPaths(path)` | 获取全部后代路径 |
| `GetPathsByPattern(pattern)` | 按通配符查询 |
| `GetPathsByTag(tag)` | 按标签查询 |
| `RemoveByTag(tag)` | 按标签删除 |
| `CreateBatch()` | 创建批量操作 |

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

### 分支命名

- `feat/` - 新功能
- `fix/` - 修复
- `refactor/` - 重构

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
