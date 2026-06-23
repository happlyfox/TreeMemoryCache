using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace TreeMemoryCache.Tests;

/// <summary>
/// P1-6:锁策略从 SupportsRecursion 改为 NoRecursion,审计持锁回调。
///
/// 之前 _structureLock 用 SupportsRecursion 是为了兼容"持锁调用户代码"场景。
/// 实际 OnCacheEntryEvicted 在 _innerCache 触发时,可能正在另一个线程的写锁内。
/// 改为 NoRecursion 后必须确保:
///   1) 持锁期间不调可能回调的 _innerCache 操作
///   2) OnCacheEntryEvicted 仍然能正确清理树索引
/// </summary>
public class LockStrategyTests
{
    [Fact]
    public void CacheExpirationTriggeringEvictionCallback_ShouldNotDeadlock()
    {
        // 即使发生过 OnCacheEntryEvicted 触发的"写锁内再写锁"路径(原 SupportsRecursion 允许),
        // 改为 NoRecursion 后不能死锁。
        // 通过设置一个短过期 + 触发扫描,反复跑多次验证。
        for (var iter = 0; iter < 20; iter++)
        {
            using var cache = new TreeMemoryCache();
            var entry = cache.SetTree($"iter:{iter}:a", 1);
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(10);
            entry.Dispose();

            // 等过期触发回调
            Thread.Sleep(50);

            // 在过期回调可能正在执行时,再写入新路径
            // 如果锁策略是 NoRecursion,且回调内重入,会抛 LockRecursionException
            cache.SetTreeValue($"iter:{iter}:b", 2);

            Assert.True(cache.TryGetTree<int>($"iter:{iter}:b", out var v));
            Assert.Equal(2, v);
        }
    }

    [Fact]
    public async Task ConcurrentSetAndRemoveTree_ShouldNotDeadlock()
    {
        // 并发 Set/Remove 反复跑,验证锁不会因为 NoRecursion 死锁
        using var cache = new TreeMemoryCache();

        var setTask = Task.Run(() =>
        {
            for (var i = 0; i < 100; i++)
            {
                cache.SetTreeValue($"path:{i}", i);
            }
        });

        var removeTask = Task.Run(() =>
        {
            for (var i = 0; i < 100; i += 2)
            {
                cache.RemoveTree($"path:{i}");
            }
        });

        // 5s 内必须完成,否则视为死锁
        try
        {
            await Task.WhenAll(setTask, removeTask).WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            Assert.Fail("并发 Set/RemoveTree 在 5s 内未完成,疑似死锁");
        }
    }
}