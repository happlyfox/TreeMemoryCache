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

    [Fact]
    public void Tag_FullWorkflow_ShouldAddIndexAndRemoveCorrectly()
    {
        using var cache = new TreeMemoryCache();

        // 设置带标签的缓存项
        cache.SetTree("Temp:Report:Monthly", "data1", "temp").Dispose();
        cache.SetTree("Temp:Report:Weekly", "data2", "temp").Dispose();
        cache.SetTree("Temp:Upload:image1", "bytes1", "temp").Dispose();
        cache.SetTree("Permanent:Config:App", "config", "permanent").Dispose();

        // 查询标签验证索引
        var taggedPaths = cache.GetPathsByTag("temp").ToList();
        Assert.Equal(3, taggedPaths.Count);
        Assert.Contains("Temp:Report:Monthly", taggedPaths);
        Assert.Contains("Temp:Report:Weekly", taggedPaths);
        Assert.Contains("Temp:Upload:image1", taggedPaths);

        var permanentPaths = cache.GetPathsByTag("permanent").ToList();
        Assert.Single(permanentPaths);

        // 按标签删除
        cache.RemoveByTag("temp");

        // 验证已删除
        Assert.False(cache.TryGetTree<string>("Temp:Report:Monthly", out _));
        Assert.False(cache.TryGetTree<string>("Temp:Report:Weekly", out _));
        Assert.False(cache.TryGetTree<byte[]>("Temp:Upload:image1", out _));
        // 验证未标签的不受影响
        Assert.True(cache.TryGetTree<string>("Permanent:Config:App", out _));

        // 验证索引已清空
        Assert.Empty(cache.GetPathsByTag("temp"));
    }

    [Fact]
    public void Tag_UpdateExistingNode_ShouldUpdateIndexCorrectly()
    {
        using var cache = new TreeMemoryCache();

        // 初次设置无标签
        cache.SetTree("Cache:Item1", "value1").Dispose();
        Assert.Empty(cache.GetPathsByTag("tag1"));

        // 更新并添加标签
        cache.SetTree("Cache:Item1", "value1", "tag1").Dispose();
        var paths = cache.GetPathsByTag("tag1").ToList();
        Assert.Single(paths);
        Assert.Contains("Cache:Item1", paths);

        // 更新标签变更
        cache.SetTree("Cache:Item1", "value2", "tag2").Dispose();
        Assert.Empty(cache.GetPathsByTag("tag1"));
        Assert.Single(cache.GetPathsByTag("tag2"));

        // 移除标签
        cache.SetTree("Cache:Item1", "value3").Dispose();
        Assert.Empty(cache.GetPathsByTag("tag2"));
    }

    [Fact]
    public void Tag_BatchOperation_ShouldSupportTag()
    {
        using var cache = new TreeMemoryCache();

        using var batch = cache.CreateBatch();
        batch.Set("Job:1:Result", "result1", "job-result");
        batch.Set("Job:2:Result", "result2", "job-result");
        batch.Set("Job:1:Log", "log1");
        batch.Execute();

        var paths = cache.GetPathsByTag("job-result").ToList();
        Assert.Equal(2, paths.Count);

        cache.RemoveByTag("job-result");
        Assert.False(cache.TryGetTree<string>("Job:1:Result", out _));
        Assert.False(cache.TryGetTree<string>("Job:2:Result", out _));
        Assert.True(cache.TryGetTree<string>("Job:1:Log", out _));
    }

    [Fact]
    public void Tag_AnyLevelNode_ShouldAllowSetTag()
    {
        using var cache = new TreeMemoryCache();

        // 在不同分支的不同层级设置标签
        cache.SetTree("RootA", "roota-value", "root-tag").Dispose();
        cache.SetTree("RootA:Level1", "level1-value", "level1-tag").Dispose();
        cache.SetTree("RootB:Level2", "level2-value", "level2-tag").Dispose();

        // 每个层级都能查询到
        Assert.Single(cache.GetPathsByTag("root-tag"));
        Assert.Single(cache.GetPathsByTag("level1-tag"));
        Assert.Single(cache.GetPathsByTag("level2-tag"));

        // 删除中间层级标签不影响其他分支的其他层级
        cache.RemoveByTag("level1-tag");
        Assert.Single(cache.GetPathsByTag("root-tag"));
        Assert.Single(cache.GetPathsByTag("level2-tag"));
        Assert.Empty(cache.GetPathsByTag("level1-tag"));
    }
}
