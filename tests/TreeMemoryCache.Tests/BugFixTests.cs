using Xunit;

namespace TreeMemoryCache.Tests;

public class BugFixTests
{
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
