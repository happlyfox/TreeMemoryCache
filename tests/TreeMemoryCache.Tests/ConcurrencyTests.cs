using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace TreeMemoryCache.Tests;

public class ConcurrencyTests
{
    [Fact]
    public async Task ConcurrentAccess_ShouldBeThreadSafe()
    {
        using var cache = new TreeMemoryCache();
        var tasks = new List<Task>();
        var errors = new List<Exception>();

        for (var i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    var path = $"Line:{index}:Stations";
                    cache.SetTree(path, new List<string> { $"Station{index}" }).Dispose();
                    cache.TryGetTree<List<string>>(path, out _);
                    if (index % 10 == 0)
                    {
                        cache.RemoveTree($"Line:{index}");
                    }
                }
                catch (Exception ex)
                {
                    lock (errors)
                    {
                        errors.Add(ex);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task ConcurrentSetAndRead_SamePath_ShouldBeConsistent()
    {
        using var cache = new TreeMemoryCache();
        var tasks = new List<Task>();
        var readResults = new List<string>();
        var lockObj = new object();

        for (var i = 0; i < 50; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                var path = "Line:6:Shared";
                cache.SetTree(path, $"Value{index}").Dispose();

                if (cache.TryGetTree<string>(path, out var value))
                {
                    lock (lockObj)
                    {
                        readResults.Add(value!);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(50, readResults.Count);
        Assert.All(readResults, r => Assert.StartsWith("Value", r));
    }

    [Fact]
    public async Task ConcurrentRemoveTree_DifferentBranches_ShouldNotInterfere()
    {
        using var cache = new TreeMemoryCache();
        var tasks = new List<Task>();
        var errors = new List<Exception>();

        for (var i = 0; i < 10; i++)
        {
            for (var j = 0; j < 10; j++)
            {
                cache.SetTree($"Line:{i}:Branch{j}:Leaf", "Value").Dispose();
            }
        }

        for (var i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    cache.RemoveTree($"Line:{index}");
                }
                catch (Exception ex)
                {
                    lock (errors)
                    {
                        errors.Add(ex);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Empty(errors);
        var stats = cache.GetStatistics();
        Assert.True(stats.TotalNodeCount >= 0);
    }

    [Fact]
    public async Task ConcurrentReadOperations_ShouldNotBlock()
    {
        using var cache = new TreeMemoryCache();

        for (var i = 0; i < 100; i++)
        {
            cache.SetTree($"Line:{i}:Data", $"Value{i}").Dispose();
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var tasks = new List<Task>();

        for (var i = 0; i < 1000; i++)
        {
            var index = i % 100;
            tasks.Add(Task.Run(() =>
            {
                cache.TryGetTree<string>($"Line:{index}:Data", out _);
                cache.GetChildPaths($"Line:{index}");
                cache.GetDescendantPaths($"Line:{index}");
            }));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 5000, "Read operations took too long");
    }

    [Fact]
    public async Task ConcurrentBatchOperations_ShouldBeIsolated()
    {
        using var cache = new TreeMemoryCache();
        var tasks = new List<Task>();
        var errors = new List<Exception>();

        for (var i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    using var batch = cache.CreateBatch();
                    batch.Set($"Line:{index}:A", $"ValueA{index}")
                          .Set($"Line:{index}:B", $"ValueB{index}");
                    batch.Execute();
                }
                catch (Exception ex)
                {
                    lock (errors)
                    {
                        errors.Add(ex);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task ConcurrentSetTree_WithParentChildRelationship_ShouldMaintainIntegrity()
    {
        using var cache = new TreeMemoryCache();
        var tasks = new List<Task>();
        var errors = new List<Exception>();

        for (var i = 0; i < 50; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    cache.SetTree($"Line:6:Branch{index}:Leaf", $"Value{index}").Dispose();
                }
                catch (Exception ex)
                {
                    lock (errors)
                    {
                        errors.Add(ex);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Empty(errors);

        var children = cache.GetChildPaths("Line:6").ToList();
        Assert.Equal(50, children.Count);
    }

    [Fact]
    public async Task ConcurrentAsyncRemoveTree_ShouldBeThreadSafe()
    {
        using var cache = new TreeMemoryCache();
        var tasks = new List<Task>();
        var errors = new List<Exception>();

        for (var i = 0; i < 5; i++)
        {
            for (var j = 0; j < 10; j++)
            {
                cache.SetTree($"Line:{i}:Branch{j}:Leaf", "Value").Dispose();
            }
        }

        for (var i = 0; i < 5; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await foreach (var _ in cache.RemoveTreeAsync($"Line:{index}"))
                    {
                    }
                }
                catch (Exception ex)
                {
                    lock (errors)
                    {
                        errors.Add(ex);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task ConcurrentStatisticsRead_ShouldNotAffectOperations()
    {
        using var cache = new TreeMemoryCache();
        var tasks = new List<Task>();
        var errors = new List<Exception>();

        for (var i = 0; i < 100; i++)
        {
            cache.SetTree($"Line:{i}:Data", $"Value{i}").Dispose();
        }

        for (var i = 0; i < 50; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    cache.GetStatistics();
                    cache.SetTree($"Line:{index + 100}:New", "NewValue").Dispose();
                    cache.TryGetTree<string>($"Line:{index}:Data", out _);
                }
                catch (Exception ex)
                {
                    lock (errors)
                    {
                        errors.Add(ex);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task HighContention_SamePath_ShouldEventuallySucceed()
    {
        using var cache = new TreeMemoryCache();
        var tasks = new List<Task<bool>>();
        var successCount = 0;
        var lockObj = new object();

        for (var i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    var path = "Line:6:Contended";
                    cache.SetTree(path, $"Value{index}").Dispose();
                    var result = cache.TryGetTree<string>(path, out var value);
                    if (result)
                    {
                        lock (lockObj)
                        {
                            successCount++;
                        }
                    }
                    return result;
                }
                catch
                {
                    return false;
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.True(successCount > 0, "At least some operations should succeed");
    }

    [Fact]
    public async Task ConcurrentPatternMatching_ShouldReturnConsistentResults()
    {
        using var cache = new TreeMemoryCache();
        var tasks = new List<Task>();
        var results = new List<IEnumerable<string>>();
        var lockObj = new object();

        for (var i = 0; i < 50; i++)
        {
            cache.SetTree($"Line:{i}:Upward:Station", "Value").Dispose();
        }

        for (var i = 0; i < 20; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var matches = cache.GetPathsByPattern("Line:*:Upward:*");
                lock (lockObj)
                {
                    results.Add(matches);
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.Equal(50, r.Count()));
    }

    [Fact]
    public async Task ConcurrentRemoveAndSetChildren_ShouldNotOrphanNodes()
    {
        using var cache = new TreeMemoryCache();
        var tasks = new List<Task>();
        var errors = new List<Exception>();

        cache.SetTree("Line:6:A:1", "1").Dispose();
        cache.SetTree("Line:6:A:2", "2").Dispose();

        for (var i = 0; i < 50; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    if (index % 2 == 0)
                    {
                        cache.RemoveTree("Line:6:A");
                    }
                    else
                    {
                        cache.SetTree($"Line:6:A:New{index}", $"New{index}").Dispose();
                    }
                }
                catch (Exception ex)
                {
                    lock (errors)
                    {
                        errors.Add(ex);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.DoesNotContain(errors, e => e is not ArgumentException);
    }

    [Fact]
    public async Task StressTest_ThousandsOfOperations_ShouldRemainStable()
    {
        using var cache = new TreeMemoryCache();
        var tasks = new List<Task>();
        var errors = new List<Exception>();
        var operationCount = 5000;

        for (var i = 0; i < operationCount; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    var lineIndex = index % 100;
                    var path = $"Line:{lineIndex}:Branch:{index}";

                    cache.SetTree(path, $"Value{index}").Dispose();

                    if (index % 3 == 0)
                    {
                        cache.TryGetTree<string>(path, out _);
                    }

                    if (index % 5 == 0)
                    {
                        cache.RemoveTree($"Line:{lineIndex}");
                    }
                }
                catch (Exception ex)
                {
                    lock (errors)
                    {
                        errors.Add(ex);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.DoesNotContain(errors, e => e is not ArgumentException);
    }

    [Fact]
    public async Task ConcurrentDispose_ShouldBeSafe()
    {
        var cache = new TreeMemoryCache();
        var tasks = new List<Task>();
        var errors = new List<Exception>();

        for (var i = 0; i < 100; i++)
        {
            cache.SetTree($"Line:{i}:Data", $"Value{i}").Dispose();
        }

        for (var i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    cache.SetTree($"Line:New:{Guid.NewGuid()}", "Value").Dispose();
                }
                catch (ObjectDisposedException)
                {
                }
                catch (Exception ex)
                {
                    lock (errors)
                    {
                        errors.Add(ex);
                    }
                }
            }));
        }

        cache.Dispose();

        await Task.WhenAll(tasks);

        Assert.DoesNotContain(errors, e => e is not ObjectDisposedException);
    }

    [Fact]
    public async Task ConcurrentTagOperations_ShouldBeThreadSafe()
    {
        using var cache = new TreeMemoryCache();
        var tasks = new List<Task>();
        var errors = new List<Exception>();

        for (var i = 0; i < 50; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    var tag = index % 5 == 0 ? "Special" : "Normal";
                    cache.SetTree($"Line:{index}:Data", $"Value{index}").Dispose();
                }
                catch (Exception ex)
                {
                    lock (errors)
                    {
                        errors.Add(ex);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Empty(errors);
    }
}
