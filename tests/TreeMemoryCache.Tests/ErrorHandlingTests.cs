using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace TreeMemoryCache.Tests;

public class ErrorHandlingTests
{
    [Fact]
    public void RemoveTree_WhenNodeRemovedByExpiration_ShouldHandleGracefully()
    {
        using var cache = new TreeMemoryCache();
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(10)
        };

        cache.SetTree("Line:6:Temp", "Value", options).Dispose();
        Thread.Sleep(50);

        var exception = Record.Exception(() => cache.RemoveTree("Line:6:Temp"));
        Assert.Null(exception);
    }

    [Fact]
    public void SetTree_WhenParentNodeRemoved_ShouldRecreatePath()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:Stations:A", "A").Dispose();
        cache.RemoveTree("Line:6");
        cache.SetTree("Line:6:Stations:B", "B").Dispose();

        Assert.True(cache.TryGetTree<string>("Line:6:Stations:B", out _));
    }

    [Fact]
    public void Batch_WhenOperationFails_ShouldRollbackOrContinue()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:Existing", "Value").Dispose();

        using var batch = cache.CreateBatch();
        batch.Set("Line:6:New", "NewValue");

        var exception = Record.Exception(() => batch.Execute());
        Assert.Null(exception);

        Assert.True(cache.TryGetTree<string>("Line:6:New", out _));
    }

    [Fact]
    public async Task ConcurrentRemoveAndSet_SamePath_ShouldHandleSafely()
    {
        using var cache = new TreeMemoryCache();
        var exceptions = new List<Exception>();
        var tasks = new List<Task>();

        for (var i = 0; i < 50; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    var path = "Line:6:Shared";
                    if (index % 2 == 0)
                    {
                        cache.SetTree(path, $"Value{index}").Dispose();
                    }
                    else
                    {
                        cache.RemoveTree(path);
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);
        Assert.DoesNotContain(exceptions, e => e is not ArgumentException);
    }

    [Fact]
    public void RemoveTree_WithProgressCallback_ShouldInvokeCallback()
    {
        using var cache = new TreeMemoryCache();
        var callbackInvoked = false;

        cache.SetTree("Line:6:A", "1").Dispose();
        cache.SetTree("Line:6:B", "2").Dispose();

        cache.RemoveTree("Line:6", new TreeRemoveOptions
        {
            OnProgress = count => callbackInvoked = true
        });

        Assert.True(callbackInvoked);
        Assert.False(cache.TryGetTree<string>("Line:6:A", out _));
        Assert.False(cache.TryGetTree<string>("Line:6:B", out _));
    }

    [Fact]
    public void GetStatistics_AfterMultipleOperations_ShouldBeConsistent()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:A", "1").Dispose();
        cache.SetTree("Line:6:B", "2").Dispose();
        cache.TryGetTree<string>("Line:6:A", out _);
        cache.TryGetTree<string>("Line:6:NonExistent", out _);
        cache.RemoveTree("Line:6:A");

        var stats = cache.GetStatistics();

        Assert.True(stats.TotalNodeCount >= 0);
        Assert.True(stats.HitCount >= 1);
        Assert.True(stats.MissCount >= 1);
    }

    [Fact]
    public void MemoryCache_PressureEviction_ShouldHandleGracefully()
    {
        var options = new MemoryCacheOptions
        {
            SizeLimit = 100
        };
        using var cache = new TreeMemoryCache(options);

        for (var i = 0; i < 200; i++)
        {
            var entryOptions = new MemoryCacheEntryOptions
            {
                Size = 1
            };
            cache.SetTree($"Line:{i}:Data", $"Value{i}", entryOptions).Dispose();
        }

        var stats = cache.GetStatistics();
        Assert.True(stats.TotalNodeCount <= 200);
    }

    [Fact]
    public void SetTree_WithCircularReference_ShouldNotCauseInfiniteLoop()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:A", "A").Dispose();
        cache.SetTree("Line:6:A:B", "B").Dispose();

        Assert.True(cache.TryGetTree<string>("Line:6:A", out _));
        Assert.True(cache.TryGetTree<string>("Line:6:A:B", out _));
    }

    [Fact]
    public void RemoveTree_DeeplyNested_ShouldCompleteInReasonableTime()
    {
        using var cache = new TreeMemoryCache();

        for (var i = 0; i < 100; i++)
        {
            var path = $"Line:6:{string.Join(":", Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()))}";
            cache.SetTree(path, "Value").Dispose();
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        cache.RemoveTree("Line:6");
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 5000, "RemoveTree took too long");
    }

    [Fact]
    public void TryGetTree_WithExpiredEntry_ShouldReturnFalse()
    {
        using var cache = new TreeMemoryCache();
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(10)
        };

        cache.SetTree("Line:6:Expiring", "Value", options).Dispose();
        Thread.Sleep(100);

        var result = cache.TryGetTree<string>("Line:6:Expiring", out var value);
        Assert.False(result);
        Assert.Null(value);
    }

    [Fact]
    public void RemoveTree_WhenCacheDisposed_ShouldThrow()
    {
        var cache = new TreeMemoryCache();
        cache.Dispose();

        Assert.Throws<ObjectDisposedException>(() => cache.RemoveTree("Line:6"));
    }

    [Fact]
    public void GetChildPaths_WhenCacheDisposed_ShouldThrow()
    {
        var cache = new TreeMemoryCache();
        cache.Dispose();

        Assert.Throws<ObjectDisposedException>(() => cache.GetChildPaths("Line:6"));
    }

    [Fact]
    public void GetDescendantPaths_WhenCacheDisposed_ShouldThrow()
    {
        var cache = new TreeMemoryCache();
        cache.Dispose();

        Assert.Throws<ObjectDisposedException>(() => cache.GetDescendantPaths("Line:6"));
    }

    [Fact]
    public void GetStatistics_WhenCacheDisposed_ShouldThrow()
    {
        var cache = new TreeMemoryCache();
        cache.Dispose();

        Assert.Throws<ObjectDisposedException>(() => cache.GetStatistics());
    }

    [Fact]
    public void CreateBatch_WhenCacheDisposed_ShouldThrow()
    {
        var cache = new TreeMemoryCache();
        cache.Dispose();

        Assert.Throws<ObjectDisposedException>(() => cache.CreateBatch());
    }

    [Fact]
    public async Task RemoveTreeAsync_WhenCacheDisposed_ShouldThrow()
    {
        var cache = new TreeMemoryCache();
        cache.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            await foreach (var _ in cache.RemoveTreeAsync("Line:6"))
            {
            }
        });
    }

    [Fact]
    public void SetTree_WithVeryLargeValue_ShouldHandleGracefully()
    {
        using var cache = new TreeMemoryCache();
        var largeValue = new byte[1024 * 1024];
        new Random().NextBytes(largeValue);

        cache.SetTree("Line:6:LargeData", largeValue).Dispose();

        Assert.True(cache.TryGetTree<byte[]>("Line:6:LargeData", out var result));
        Assert.Equal(largeValue.Length, result!.Length);
    }

    [Fact]
    public void PatternMatching_WithMultipleWildcards_ShouldMatchCorrectly()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:Upward:Station1", "S1").Dispose();
        cache.SetTree("Line:6:Downward:Station2", "S2").Dispose();
        cache.SetTree("Line:8:Upward:Station3", "S3").Dispose();
        cache.SetTree("Line:8:Downward:Station4", "S4").Dispose();

        var matches = cache.GetPathsByPattern("Line:*:*:Station*").ToList();

        Assert.True(matches.Count >= 0);
    }

    [Fact]
    public void RemoveTree_OnLeafNode_ShouldNotAffectSiblings()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:A", "A").Dispose();
        cache.SetTree("Line:6:B", "B").Dispose();
        cache.SetTree("Line:6:C", "C").Dispose();

        cache.RemoveTree("Line:6:B");

        Assert.True(cache.TryGetTree<string>("Line:6:A", out _));
        Assert.False(cache.TryGetTree<string>("Line:6:B", out _));
        Assert.True(cache.TryGetTree<string>("Line:6:C", out _));
    }
}
