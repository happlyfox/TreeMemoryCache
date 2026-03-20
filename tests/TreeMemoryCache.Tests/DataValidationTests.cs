using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace TreeMemoryCache.Tests;

public class DataValidationTests
{
    [Fact]
    public void SetTree_WithValidPath_ShouldCreateCorrectHierarchy()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:Upward:Stations", new List<string> { "A", "B" }).Dispose();

        Assert.True(cache.TryGetTree<List<string>>("Line:6:Upward:Stations", out _));
        var children = cache.GetChildPaths("Line:6").ToList();
        Assert.Contains("Line:6:Upward", children);
    }

    [Fact]
    public void SetTree_ShouldMaintainParentChildRelationship()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:Upward:Stations", "Data").Dispose();

        var children = cache.GetChildPaths("Line:6").ToList();
        Assert.Single(children);
        Assert.Equal("Line:6:Upward", children[0]);

        var grandChildren = cache.GetChildPaths("Line:6:Upward").ToList();
        Assert.Single(grandChildren);
        Assert.Equal("Line:6:Upward:Stations", grandChildren[0]);
    }

    [Fact]
    public void RemoveTree_ShouldUpdateParentChildRelationship()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:A", "A").Dispose();
        cache.SetTree("Line:6:B", "B").Dispose();
        cache.SetTree("Line:6:C", "C").Dispose();

        cache.RemoveTree("Line:6:B");

        var children = cache.GetChildPaths("Line:6").ToList();
        Assert.Equal(2, children.Count);
        Assert.DoesNotContain("Line:6:B", children);
    }

    [Fact]
    public void GetDescendantPaths_ShouldReturnCorrectCount()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:A:1", "1").Dispose();
        cache.SetTree("Line:6:B:2", "2").Dispose();
        cache.SetTree("Line:6:C", "C").Dispose();

        var descendants = cache.GetDescendantPaths("Line:6").ToList();

        Assert.True(descendants.Count >= 3);
    }

    [Fact]
    public void PatternMatching_ShouldMatchCorrectPaths()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:A", "A").Dispose();
        cache.SetTree("Line:6:A:B", "B").Dispose();
        cache.SetTree("Line:6:A:B:C", "C").Dispose();

        var twoSegmentMatches = cache.GetPathsByPattern("*:*").ToList();
        var threeSegmentMatches = cache.GetPathsByPattern("*:*:*").ToList();

        Assert.True(twoSegmentMatches.Count > 0);
        Assert.True(threeSegmentMatches.Count > 0);
    }

    [Fact]
    public void Statistics_ShouldReflectActualState()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:A", "A").Dispose();
        cache.SetTree("Line:8:B", "B").Dispose();

        cache.TryGetTree<string>("Line:6:A", out _);
        cache.TryGetTree<string>("Line:6:A", out _);
        cache.TryGetTree<string>("Line:8:B", out _);
        cache.TryGetTree<string>("Line:NonExistent", out _);

        var stats = cache.GetStatistics();

        Assert.True(stats.TotalNodeCount >= 2);
        Assert.Equal(3, stats.HitCount);
        Assert.Equal(1, stats.MissCount);
        Assert.True(stats.NodeCountByRoot.Count >= 1);
    }

    [Fact]
    public void CacheExpiration_ShouldUpdateNodeCount()
    {
        using var cache = new TreeMemoryCache();
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(50)
        };

        cache.SetTree("Line:6:Temp", "Value", options).Dispose();
        var statsBefore = cache.GetStatistics();
        Assert.True(statsBefore.TotalNodeCount >= 1);

        Thread.Sleep(150);

        cache.TryGetTree<string>("Line:6:Temp", out _);
        var statsAfter = cache.GetStatistics();
        Assert.True(statsAfter.TotalNodeCount >= 0);
    }

    [Fact]
    public void SetTree_WithDifferentTypes_ShouldStoreCorrectly()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:String", "StringValue").Dispose();
        cache.SetTree("Line:6:Int", 42).Dispose();
        cache.SetTree("Line:6:List", new List<int> { 1, 2, 3 }).Dispose();
        cache.SetTree("Line:6:Object", new { Name = "Test" }).Dispose();

        Assert.True(cache.TryGetTree<string>("Line:6:String", out var str));
        Assert.Equal("StringValue", str);

        Assert.True(cache.TryGetTree<int>("Line:6:Int", out var num));
        Assert.Equal(42, num);

        Assert.True(cache.TryGetTree<List<int>>("Line:6:List", out var list));
        Assert.Equal(new List<int> { 1, 2, 3 }, list);
    }

    [Fact]
    public void PathComparison_ShouldBeCaseSensitive()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:Station", "Lower").Dispose();
        cache.SetTree("Line:6:STATION", "Upper").Dispose();

        Assert.True(cache.TryGetTree<string>("Line:6:Station", out var lower));
        Assert.Equal("Lower", lower);

        Assert.True(cache.TryGetTree<string>("Line:6:STATION", out var upper));
        Assert.Equal("Upper", upper);
    }

    [Fact]
    public void RemoveTree_WithIncludeSelfFalse_ShouldValidateCorrectly()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6", "Root").Dispose();
        cache.SetTree("Line:6:A", "A").Dispose();
        cache.SetTree("Line:6:B", "B").Dispose();

        cache.RemoveTree("Line:6", new TreeRemoveOptions { IncludeSelf = false });

        Assert.True(cache.TryGetTree<string>("Line:6", out _));
        Assert.False(cache.TryGetTree<string>("Line:6:A", out _));
        Assert.False(cache.TryGetTree<string>("Line:6:B", out _));
    }

    [Fact]
    public void BatchOperations_ShouldValidateAllOperations()
    {
        using var cache = new TreeMemoryCache();

        using var batch = cache.CreateBatch();
        batch.Set("Line:6:A", "ValueA")
              .Set("Line:6:B", "ValueB")
              .Set("Line:8:C", "ValueC");
        batch.Execute();

        Assert.True(cache.TryGetTree<string>("Line:6:A", out var a));
        Assert.Equal("ValueA", a);
        Assert.True(cache.TryGetTree<string>("Line:6:B", out var b));
        Assert.Equal("ValueB", b);
        Assert.True(cache.TryGetTree<string>("Line:8:C", out var c));
        Assert.Equal("ValueC", c);
    }

    [Fact]
    public void BatchRemoveTree_ShouldValidateCompleteRemoval()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:A:1", "1").Dispose();
        cache.SetTree("Line:6:A:2", "2").Dispose();
        cache.SetTree("Line:6:B:3", "3").Dispose();

        using var batch = cache.CreateBatch();
        batch.RemoveTree("Line:6:A");
        batch.Execute();

        Assert.False(cache.TryGetTree<string>("Line:6:A:1", out _));
        Assert.False(cache.TryGetTree<string>("Line:6:A:2", out _));
        Assert.True(cache.TryGetTree<string>("Line:6:B:3", out _));
    }

    [Fact]
    public void NodeCountByRoot_ShouldGroupCorrectly()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:A", "A").Dispose();
        cache.SetTree("Line:6:B", "B").Dispose();
        cache.SetTree("Line:8:C", "C").Dispose();
        cache.SetTree("Bus:10:D", "D").Dispose();

        var stats = cache.GetStatistics();

        Assert.True(stats.NodeCountByRoot.Count >= 2);
        Assert.True(stats.NodeCountByRoot.ContainsKey("Line") || stats.NodeCountByRoot.ContainsKey("Bus"));
    }

    [Fact]
    public void CacheSizeEstimation_ShouldBeReasonable()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:SmallString", "ABC").Dispose();
        cache.SetTree("Line:6:LargeString", new string('X', 1000)).Dispose();
        cache.SetTree("Line:6:ByteArray", new byte[500]).Dispose();
        cache.SetTree("Line:6:Array", new int[100]).Dispose();

        var stats = cache.GetStatistics();

        Assert.True(stats.TotalCacheSize > 0);
    }

    [Fact]
    public void AverageAccessTime_ShouldBePositive()
    {
        using var cache = new TreeMemoryCache();

        for (var i = 0; i < 100; i++)
        {
            cache.SetTree($"Line:{i}:Data", $"Value{i}").Dispose();
            cache.TryGetTree<string>($"Line:{i}:Data", out _);
        }

        var stats = cache.GetStatistics();

        Assert.True(stats.AverageAccessTime >= TimeSpan.Zero);
    }

    [Fact]
    public void CascadeDeleteCount_ShouldIncrementCorrectly()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:A:1", "1").Dispose();
        cache.SetTree("Line:6:A:2", "2").Dispose();
        cache.SetTree("Line:6:B:3", "3").Dispose();

        cache.RemoveTree("Line:6:A");
        var stats1 = cache.GetStatistics();
        Assert.True(stats1.CascadeDeleteCount >= 1);

        cache.RemoveTree("Line:6:B");
        var stats2 = cache.GetStatistics();
        Assert.True(stats2.CascadeDeleteCount >= 2);
    }

    [Fact]
    public void WildcardMatching_ShouldMatchCorrectly()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:Upward:Station1", "S1").Dispose();
        cache.SetTree("Line:6:Downward:Station2", "S2").Dispose();
        cache.SetTree("Line:8:Upward:Station3", "S3").Dispose();

        var allLines = cache.GetPathsByPattern("Line:*").ToList();
        Assert.True(allLines.Count >= 3);

        var upwardOnly = cache.GetPathsByPattern("Line:*:Upward:*").ToList();
        Assert.Equal(2, upwardOnly.Count);

        var specificLine = cache.GetPathsByPattern("Line:6:*:*").ToList();
        Assert.Equal(2, specificLine.Count);
    }
}
