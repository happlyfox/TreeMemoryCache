using Xunit;

namespace TreeMemoryCache.Tests;

public class BugFixTests
{
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
    {
        using var cache = new TreeMemoryCache();

        using var batch = cache.CreateBatch();
        batch.Set("Test:Path", "Value");

        // 如果有未执行的操作，Dispose 应该抛出异常
        Assert.Throws<InvalidOperationException>(() => batch.Dispose());
    }

    [Fact]
    public void Batch_DisposeAfterExecute_ShouldNotThrow()
    {
        using var cache = new TreeMemoryCache();

        using var batch = cache.CreateBatch();
        batch.Set("Test:Path", "Value");
        batch.Execute();

        // Execute 后 Dispose 不应抛出异常
        var exception = Record.Exception(() => batch.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void Batch_DisposeEmpty_ShouldNotThrow()
    {
        using var cache = new TreeMemoryCache();

        using var batch = cache.CreateBatch();

        // 空 batch Dispose 不应抛出异常
        var exception = Record.Exception(() => batch.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public async Task GetPathsByPattern_ConcurrentWrite_ShouldNotThrow()
    {
        using var cache = new TreeMemoryCache();
        var errors = new List<Exception>();

        // 创建一些数据
        for (int i = 0; i < 50; i++)
        {
            cache.SetTree($"Line:{i}:Upward:Station", $"S{i}").Dispose();
        }

        var readTasks = new List<Task>();
        var writeTasks = new List<Task>();

        // 读任务
        for (int t = 0; t < 5; t++)
        {
            readTasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < 20; i++)
                    {
                        _ = cache.GetPathsByPattern("Line:*:Upward:*").ToList();
                    }
                }
                catch (Exception ex)
                {
                    lock (errors) { errors.Add(ex); }
                }
            }));
        }

        // 写任务
        for (int t = 0; t < 5; t++)
        {
            writeTasks.Add(Task.Run(() =>
            {
                for (int i = 50; i < 70; i++)
                {
                    cache.SetTree($"Line:{i}:Upward:Station", $"S{i}").Dispose();
                }
            }));
        }

        await Task.WhenAll([.. readTasks, .. writeTasks]);

        Assert.Empty(errors);
    }
}
