using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace TreeMemoryCache.Tests;

public class NormalBusinessFlowTests
{
    [Fact]
    public void SetTree_ShouldCreateNodeInCache()
    {
        using var cache = new TreeMemoryCache();
        var path = "Line:6:Upward:Stations";
        var value = new List<string> { "Station1", "Station2", "Station3" };

        var entry = cache.SetTree(path, value);
        entry.Dispose();

        Assert.True(cache.TryGetTree<List<string>>(path, out var result));
        Assert.Equal(value, result);
    }

    [Fact]
    public void SetTree_ShouldCreateParentNodes()
    {
        using var cache = new TreeMemoryCache();
        var path = "Line:6:Upward:Stations";
        var value = new List<string> { "Station1", "Station2" };

        var entry = cache.SetTree(path, value);
        entry.Dispose();

        var children = cache.GetChildPaths("Line:6");
        Assert.Contains("Line:6:Upward", children);
    }

    [Fact]
    public void SetTree_ShouldUpdateExistingValue()
    {
        using var cache = new TreeMemoryCache();
        var path = "Line:6:Name";

        cache.SetTree(path, "OldName").Dispose();
        cache.SetTree(path, "NewName").Dispose();

        Assert.True(cache.TryGetTree<string>(path, out var result));
        Assert.Equal("NewName", result);
    }

    [Fact]
    public void RemoveTree_ShouldRemoveAllDescendants()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:Upward:Stations", new List<string> { "A", "B" }).Dispose();
        cache.SetTree("Line:6:Downward:Stations", new List<string> { "C", "D" }).Dispose();
        cache.SetTree("Line:8:Upward:Stations", new List<string> { "E", "F" }).Dispose();

        cache.RemoveTree("Line:6");

        Assert.False(cache.TryGetTree<List<string>>("Line:6:Upward:Stations", out _));
        Assert.False(cache.TryGetTree<List<string>>("Line:6:Downward:Stations", out _));
        Assert.True(cache.TryGetTree<List<string>>("Line:8:Upward:Stations", out _));
    }

    [Fact]
    public void RemoveTree_WithIncludeSelfFalse_ShouldKeepParent()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:Upward:Stations", new List<string> { "A", "B" }).Dispose();
        cache.SetTree("Line:6", "Line6Info").Dispose();

        cache.RemoveTree("Line:6", new TreeRemoveOptions { IncludeSelf = false });

        Assert.True(cache.TryGetTree<string>("Line:6", out _));
        Assert.False(cache.TryGetTree<List<string>>("Line:6:Upward:Stations", out _));
    }

    [Fact]
    public void GetDescendantPaths_ShouldReturnAllDescendants()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:Upward:Stations", new List<string>()).Dispose();
        cache.SetTree("Line:6:Downward:Stations", new List<string>()).Dispose();
        cache.SetTree("Line:6:Config", new object()).Dispose();

        var descendants = cache.GetDescendantPaths("Line:6").ToList();

        Assert.True(descendants.Count >= 3);
        Assert.Contains("Line:6:Upward", descendants);
        Assert.Contains("Line:6:Downward", descendants);
        Assert.Contains("Line:6:Config", descendants);
    }

    [Fact]
    public void GetChildPaths_ShouldReturnOnlyDirectChildren()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:Upward:Stations", new List<string>()).Dispose();
        cache.SetTree("Line:6:Downward:Stations", new List<string>()).Dispose();

        var children = cache.GetChildPaths("Line:6").ToList();

        Assert.Equal(2, children.Count);
        Assert.Contains("Line:6:Upward", children);
        Assert.Contains("Line:6:Downward", children);
    }

    [Fact]
    public void GetPathsByPattern_ShouldMatchWildcards()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:Upward:Stations", new List<string>()).Dispose();
        cache.SetTree("Line:6:Downward:Stations", new List<string>()).Dispose();
        cache.SetTree("Line:8:Upward:Stations", new List<string>()).Dispose();

        var matches = cache.GetPathsByPattern("Line:*:Upward:*").ToList();

        Assert.Equal(2, matches.Count);
        Assert.Contains("Line:6:Upward:Stations", matches);
        Assert.Contains("Line:8:Upward:Stations", matches);
    }

    [Fact]
    public void BatchOperations_ShouldExecuteAllOperationsAtomically()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:Old", "OldValue").Dispose();

        using var batch = cache.CreateBatch();
        batch.Set("Line:6:New", "NewValue")
              .Remove("Line:6:Old")
              .Set("Line:8:Test", "TestValue");
        batch.Execute();

        Assert.False(cache.TryGetTree<string>("Line:6:Old", out _));
        Assert.True(cache.TryGetTree<string>("Line:6:New", out var value));
        Assert.Equal("NewValue", value);
        Assert.True(cache.TryGetTree<string>("Line:8:Test", out _));
    }

    [Fact]
    public void GetStatistics_ShouldReturnCorrectMetrics()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:Stations", new List<string> { "A", "B" }).Dispose();
        cache.SetTree("Line:8:Stations", new List<string> { "C", "D" }).Dispose();

        cache.TryGetTree<List<string>>("Line:6:Stations", out _);
        cache.TryGetTree<List<string>>("Line:6:Stations", out _);
        cache.TryGetTree<List<string>>("Line:NonExistent", out _);

        var stats = cache.GetStatistics();

        Assert.True(stats.TotalNodeCount >= 2);
        Assert.Equal(2, stats.HitCount);
        Assert.Equal(1, stats.MissCount);
    }

    [Fact]
    public async Task RemoveTreeAsync_ShouldRemoveAllNodes()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:Upward:Stations", new List<string>()).Dispose();
        cache.SetTree("Line:6:Downward:Stations", new List<string>()).Dispose();

        var removedPaths = new List<string>();
        await foreach (var path in cache.RemoveTreeAsync("Line:6"))
        {
            removedPaths.Add(path);
        }

        Assert.True(removedPaths.Count >= 2);
        Assert.False(cache.TryGetTree<List<string>>("Line:6:Upward:Stations", out _));
    }

    [Fact]
    public void DeepTree_ShouldHandleMultipleLevels()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:Upward:Segment1:Station1", "S1").Dispose();
        cache.SetTree("Line:6:Upward:Segment1:Station2", "S2").Dispose();
        cache.SetTree("Line:6:Upward:Segment2:Station3", "S3").Dispose();
        cache.SetTree("Line:6:Downward:Segment1:Station4", "S4").Dispose();

        cache.RemoveTree("Line:6:Upward");

        Assert.False(cache.TryGetTree<string>("Line:6:Upward:Segment1:Station1", out _));
        Assert.False(cache.TryGetTree<string>("Line:6:Upward:Segment2:Station3", out _));
        Assert.True(cache.TryGetTree<string>("Line:6:Downward:Segment1:Station4", out _));
    }

    [Fact]
    public void RemoveProgress_ShouldInvokeCallback()
    {
        using var cache = new TreeMemoryCache();
        var progressCount = 0;

        cache.SetTree("Line:6:A", "1").Dispose();
        cache.SetTree("Line:6:B", "2").Dispose();
        cache.SetTree("Line:6:C", "3").Dispose();

        cache.RemoveTree("Line:6", new TreeRemoveOptions
        {
            OnProgress = count => progressCount = count
        });

        Assert.True(progressCount > 0);
    }

    [Fact]
    public void SetTree_WithExpirationOptions_ShouldApplyCorrectly()
    {
        using var cache = new TreeMemoryCache();
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = DateTimeOffset.UtcNow.AddHours(1),
            SlidingExpiration = TimeSpan.FromMinutes(30),
            Priority = CacheItemPriority.High
        };

        var entry = cache.SetTree("Line:6:Config", "ConfigValue", options);
        entry.Dispose();

        Assert.True(cache.TryGetTree<string>("Line:6:Config", out _));
    }

    [Fact]
    public void RemoveByTag_WhenTagNotUsed_ShouldNotAffectCache()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:Config", "Config1").Dispose();
        cache.SetTree("Line:8:Config", "Config2").Dispose();
        cache.SetTree("Line:6:Stations", "Stations").Dispose();

        cache.RemoveByTag("NonExistentTag");

        Assert.True(cache.TryGetTree<string>("Line:6:Config", out _));
        Assert.True(cache.TryGetTree<string>("Line:8:Config", out _));
        Assert.True(cache.TryGetTree<string>("Line:6:Stations", out _));
    }
}
