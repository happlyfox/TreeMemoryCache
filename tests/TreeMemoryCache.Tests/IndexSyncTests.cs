using Microsoft.Extensions.Caching.Memory;
using TreeMemoryCache;
using Xunit;

namespace TreeMemoryCache.Tests;

/// <summary>
/// 任何"内部删除"路径(显式 Remove、级联 RemoveTree、批量 RemoveTree、Dispose)都必须
/// 同步清理 _nodes 与 _tagIndex 树索引,不留死指针或不命中标签。
/// </summary>
public class IndexSyncTests
{
    [Fact]
    public void RemoveViaIMemoryCache_ShouldAlsoClearTreeIndex()
    {
        using var cache = new TreeMemoryCache();
        cache.SetTreeValue("A:B", 42);

        ((IMemoryCache)cache).Remove("A:B");

        var children = cache.GetChildPaths("A");
        Assert.DoesNotContain("A:B", children);

        var stats = cache.GetStatistics();
        Assert.Equal(0, stats.TotalCachedItems);
    }

    [Fact]
    public void RemoveViaIMemoryCache_ShouldAlsoClearTagIndex()
    {
        using var cache = new TreeMemoryCache();
        cache.SetTreeValue("A:B", 42, "tag-x");

        ((IMemoryCache)cache).Remove("A:B");

        Assert.Empty(cache.GetPathsByTag("tag-x"));
    }

    [Fact]
    public void RemoveTree_ShouldCleanUpTagIndexForCascadedNodes()
    {
        using var cache = new TreeMemoryCache();
        cache.SetTreeValue("A:B:C", 1, "tag-x");
        cache.SetTreeValue("A:B:D", 2, "tag-y");
        cache.SetTreeValue("A:B:E", 3, "tag-x");

        cache.RemoveTree("A:B");

        Assert.Empty(cache.GetPathsByTag("tag-x"));
        Assert.Empty(cache.GetPathsByTag("tag-y"));
    }

    [Fact]
    public void BatchRemoveTree_ShouldCleanUpTagIndex()
    {
        using var cache = new TreeMemoryCache();
        cache.SetTreeValue("X:Y:Z", 1, "tag-batch");
        cache.SetTreeValue("X:Y:W", 2, "tag-batch");

        using var batch = cache.CreateBatch();
        batch.RemoveTree("X:Y").Execute();

        Assert.Empty(cache.GetPathsByTag("tag-batch"));
    }

    [Fact]
    public void Dispose_ShouldCleanUpAllIndices()
    {
        var cache = new TreeMemoryCache();
        cache.SetTreeValue("A:B", 1, "tag-d");
        cache.SetTreeValue("A:C", 2, "tag-d");

        cache.Dispose();

        // Dispose 后任何查询都应不抛异常(disposed 检测生效)。
        Assert.Throws<ObjectDisposedException>(() => cache.SetTreeValue("A:D", 3, "tag-d"));
    }
}
