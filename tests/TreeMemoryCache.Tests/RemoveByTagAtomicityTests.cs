using System.Threading.Tasks;
using TreeMemoryCache;
using Xunit;

namespace TreeMemoryCache.Tests;

/// <summary>
/// RemoveByTag 的语义应是"按标签原子删除":
/// 写锁内先快照标签对应路径,再依次清树索引与 _innerCache,避免与并发 SetTree 改标签产生竞态。
/// </summary>
public class RemoveByTagAtomicityTests
{
    [Fact]
    public async Task RemoveByTag_ConcurrentSetTreeChangingTag_ShouldAtomicallyDelete()
    {
        // 并发 race 的概率由 OS 调度决定,外层循环仅用于增加命中窗口的机会。
        // 修复前因有 race window 会偶发失败;修复后写锁隔离两个操作,稳定通过。
        const int iterations = 50;
        for (var iter = 0; iter < iterations; iter++)
        {
            using var cache = new TreeMemoryCache();

            // 起始状态:两条路径都属于 "tag-x"
            cache.SetTreeValue("p1", "v1", "tag-x");
            cache.SetTreeValue("p2", "v2", "tag-x");

            // 启动并发任务:在线程 A 的 RemoveByTag 进行中,
            // 线程 B 把 p2 的 tag 改成 "tag-y"。
            // Thread.Yield() 主动让出当前时间片,提高两个任务交错的概率。
            var removeTask = Task.Run(() => cache.RemoveByTag("tag-x"));
            var changeTagTask = Task.Run(() =>
            {
                Thread.Yield();
                cache.SetTreeValue("p2", "v2-changed", "tag-y");
            });

            await Task.WhenAll(removeTask, changeTagTask);

            // 不变量:
            //   - "tag-x" 在 _tagIndex 里为空
            //   - 任何标 tag-x 的路径都已从 _innerCache 移除
            //   - p1 必不存在(它是 tag-x,RemoveByTag 必删)
            Assert.False(cache.TryGetTree<string>("p1", out _),
                $"iter {iter}: p1 仍标 tag-x 但没被 RemoveByTag 删除");

            var tagXPaths = cache.GetPathsByTag("tag-x").ToList();
            Assert.Empty(tagXPaths);
        }
    }

    [Fact]
    public void RemoveByTag_AfterSet_ShouldRemoveAllPathsCurrentlyTagged()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTreeValue("A:1", "v1", "tag-x");
        cache.SetTreeValue("A:2", "v2", "tag-x");
        cache.SetTreeValue("B:1", "v3", "tag-y");

        cache.RemoveByTag("tag-x");

        Assert.False(cache.TryGetTree<string>("A:1", out _));
        Assert.False(cache.TryGetTree<string>("A:2", out _));
        Assert.True(cache.TryGetTree<string>("B:1", out _));
        Assert.Empty(cache.GetPathsByTag("tag-x"));
    }

    [Fact]
    public void RemoveByTag_NonExistentTag_ShouldBeNoOp()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTreeValue("A:1", "v1", "tag-x");

        cache.RemoveByTag("non-existent");

        Assert.True(cache.TryGetTree<string>("A:1", out _));
        Assert.Equal(new[] { "A:1" }, cache.GetPathsByTag("tag-x"));
    }
}
