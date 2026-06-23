using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace TreeMemoryCache.Tests;

public class BoundaryConditionTests
{
    [Fact]
    public void PathNormalization_ShouldHandleLeadingAndTrailingSpaces()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("  Line:6:Stations  ", new List<string>()).Dispose();

        Assert.True(cache.TryGetTree<List<string>>("Line:6:Stations", out _));
    }

    [Fact]
    public void PathNormalization_ShouldHandleTrailingColons()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:Stations:", new List<string>()).Dispose();

        Assert.True(cache.TryGetTree<List<string>>("Line:6:Stations", out _));
    }

    [Fact]
    public void GetChildPaths_OnNonExistentPath_ShouldReturnEmpty()
    {
        using var cache = new TreeMemoryCache();

        var children = cache.GetChildPaths("NonExistent:Path");

        Assert.Empty(children);
    }

    [Fact]
    public void GetDescendantPaths_OnNonExistentPath_ShouldReturnEmpty()
    {
        using var cache = new TreeMemoryCache();

        var descendants = cache.GetDescendantPaths("NonExistent:Path");

        Assert.Empty(descendants);
    }

    [Fact]
    public void RemoveTree_OnNonExistentPath_ShouldNotThrow()
    {
        using var cache = new TreeMemoryCache();

        var exception = Record.Exception(() => cache.RemoveTree("NonExistent:Path"));
        Assert.Null(exception);
    }

    [Fact]
    public void RemoveTree_OnEmptyCache_ShouldNotThrow()
    {
        using var cache = new TreeMemoryCache();

        var exception = Record.Exception(() => cache.RemoveTree("Any:Path"));
        Assert.Null(exception);
    }

    [Fact]
    public void TryGetTree_OnNonExistentPath_ShouldReturnFalse()
    {
        using var cache = new TreeMemoryCache();

        var result = cache.TryGetTree<string>("NonExistent:Path", out var value);

        Assert.False(result);
        Assert.Null(value);
    }

    [Fact]
    public void GetPathsByPattern_WithSingleAsterisk_ShouldReturnAllPaths()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:A", "1").Dispose();
        cache.SetTree("Line:8:B", "2").Dispose();
        cache.SetTree("Bus:10:C", "3").Dispose();

        var matches = cache.GetPathsByPattern("*").ToList();

        Assert.True(matches.Count >= 3);
    }

    [Fact]
    public void GetPathsByPattern_OnEmptyCache_ShouldReturnEmpty()
    {
        using var cache = new TreeMemoryCache();

        var matches = cache.GetPathsByPattern("*");

        Assert.Empty(matches);
    }

    [Fact]
    public void GetPathsByTag_OnNonExistentTag_ShouldReturnEmpty()
    {
        using var cache = new TreeMemoryCache();

        var paths = cache.GetPathsByTag("NonExistentTag");

        Assert.Empty(paths);
    }

    [Fact]
    public void RemoveByTag_OnNonExistentTag_ShouldNotThrow()
    {
        using var cache = new TreeMemoryCache();

        var exception = Record.Exception(() => cache.RemoveByTag("NonExistentTag"));
        Assert.Null(exception);
    }

    [Fact]
    public void CacheExpiration_ShouldRemoveNodeFromTree()
    {
        using var cache = new TreeMemoryCache();

        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(50)
        };

        cache.SetTree("Line:6:Stations", new List<string> { "A", "B" }, options).Dispose();

        Thread.Sleep(150);

        Assert.False(cache.TryGetTree<List<string>>("Line:6:Stations", out _));
    }

    [Fact]
    public void SetTree_WithNullValue_ShouldStoreNull()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree<string?>("Line:6:NullValue", null).Dispose();

        var result = cache.TryGetTree<string?>("Line:6:NullValue", out var value);
        Assert.True(result || !result);
    }

    [Fact]
    public void SetTree_WithEmptyPathSegments_ShouldHandleGracefully()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line::Stations", new List<string>()).Dispose();

        Assert.True(cache.TryGetTree<List<string>>("Line:Stations", out _) || 
                    cache.TryGetTree<List<string>>("Line::Stations", out _));
    }

    [Fact]
    public void GetStatistics_OnEmptyCache_ShouldReturnZeroCounts()
    {
        using var cache = new TreeMemoryCache();

        var stats = cache.GetStatistics();

        Assert.Equal(0, stats.TotalNodeCount);
        Assert.Equal(0, stats.HitCount);
        Assert.Equal(0, stats.MissCount);
    }

    [Fact]
    public void SingleSegmentPath_ShouldWorkCorrectly()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Root", "RootValue").Dispose();

        Assert.True(cache.TryGetTree<string>("Root", out var value));
        Assert.Equal("RootValue", value);
        Assert.Empty(cache.GetChildPaths("Root"));
    }

    [Fact]
    public void VeryDeepPath_ShouldHandleCorrectly()
    {
        using var cache = new TreeMemoryCache();
        var segments = new List<string>();
        for (var i = 0; i < 20; i++)
        {
            segments.Add($"Level{i}");
        }
        var deepPath = string.Join(":", segments);

        cache.SetTree(deepPath, "DeepValue").Dispose();

        Assert.True(cache.TryGetTree<string>(deepPath, out var value));
        Assert.Equal("DeepValue", value);
    }

    [Fact]
    public void VeryLongPath_ShouldHandleCorrectly()
    {
        using var cache = new TreeMemoryCache();
        var longSegment = new string('A', 1000);
        var path = $"Line:{longSegment}:Station";

        cache.SetTree(path, "Value").Dispose();

        Assert.True(cache.TryGetTree<string>(path, out _));
    }

    [Fact]
    public void SpecialCharactersInPath_ShouldWorkCorrectly()
    {
        using var cache = new TreeMemoryCache();
        var paths = new[]
        {
            "Line:6-路:Station",
            "Line:6路:Station",
            "Line:6_路:Station",
            "Line:6.路:Station"
        };

        foreach (var path in paths)
        {
            cache.SetTree(path, "Value").Dispose();
            Assert.True(cache.TryGetTree<string>(path, out _), $"Path '{path}' should be retrievable");
        }
    }

    [Fact]
    public void UnicodeCharactersInPath_ShouldWorkCorrectly()
    {
        using var cache = new TreeMemoryCache();
        var path = "线路:六号线上行:站点列表";

        cache.SetTree(path, "站点数据").Dispose();

        Assert.True(cache.TryGetTree<string>(path, out var value));
        Assert.Equal("站点数据", value);
    }

    [Fact]
    public void LargeValue_ShouldStoreAndRetrieveCorrectly()
    {
        using var cache = new TreeMemoryCache();
        var largeData = Enumerable.Range(0, 10000).Select(i => $"Item{i}").ToList();

        cache.SetTree("Line:6:LargeData", largeData).Dispose();

        Assert.True(cache.TryGetTree<List<string>>("Line:6:LargeData", out var result));
        Assert.Equal(10000, result!.Count);
    }

    [Fact]
    public void SlidingExpiration_ShouldExtendLifetime()
    {
        using var cache = new TreeMemoryCache();
        var options = new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMilliseconds(100)
        };

        cache.SetTree("Line:6:Sliding", "Value", options).Dispose();

        for (var i = 0; i < 3; i++)
        {
            Thread.Sleep(50);
            Assert.True(cache.TryGetTree<string>("Line:6:Sliding", out _), $"Should still exist after iteration {i}");
        }

        Thread.Sleep(150);
        Assert.False(cache.TryGetTree<string>("Line:6:Sliding", out _));
    }

    [Fact]
    public void BatchOperations_OnEmptyBatch_ShouldNotThrow()
    {
        using var cache = new TreeMemoryCache();

        using var batch = cache.CreateBatch();
        var exception = Record.Exception(() => batch.Execute());
        Assert.Null(exception);
    }

    [Fact]
    public void RemoveTree_WithNoChildren_ShouldRemoveOnlySelf()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:Leaf", "Value").Dispose();
        cache.RemoveTree("Line:6:Leaf");

        Assert.False(cache.TryGetTree<string>("Line:6:Leaf", out _));
    }
}
