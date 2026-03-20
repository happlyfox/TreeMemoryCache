---
alwaysApply: false
description: 
---
1. 项目概况
目标框架: .NET 9.0 (net9.0)
核心功能: 对 Microsoft.Extensions.Caching.Memory (IMemoryCache) 进行高级封装和扩展。
日志系统: 使用 Serilog 进行结构化日志记录。
设计原则: 高性能、线程安全、零分配（尽可能）、依赖注入友好。
2. 技术栈与依赖
基础库: Microsoft.Extensions.Caching.Memory (原生)
日志库: Serilog, Serilog.Extensions.Logging, Serilog.Sinks.Console (开发环境), Serilog.Sinks.File (生产环境)
语言特性: 充分利用 C# 13 (.NET 9) 新特性（如 params collection，改进的锁机制等）。