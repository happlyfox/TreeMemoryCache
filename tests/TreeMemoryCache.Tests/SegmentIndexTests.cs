using Xunit;

namespace TreeMemoryCache.Tests;

/// <summary>
/// P1-5:第一段索引优化通配符匹配。
///
/// 之前 GetPathsByPattern / GetDescendantPaths 对每个查询都做全表扫描。
/// 引入"按第一段分桶"的索引后,Pattern("A:*")/GetChildPaths 只需要在 A 桶内
/// 查找,不需要遍历 _nodes 全表。
///
/// 这些测试覆盖"段索引"加入后行为不变 + 段索引本身的一致性。
/// </summary>
public class SegmentIndexTests
{
    [Fact]
    public void SetTree_ThenGetPathsByPattern_ShouldReturnMatchedPaths()
    {
        using var cache = new TreeMemoryCache();
        cache.SetTreeValue("A:1", 1);
        cache.SetTreeValue("A:2", 2);
        cache.SetTreeValue("B:1", 3);

        var aPaths = cache.GetPathsByPattern("A:*").ToList();
        Assert.Equal(2, aPaths.Count);
        Assert.Contains("A:1", aPaths);
        Assert.Contains("A:2", aPaths);
    }

    [Fact]
    public void SetTree_DeepPath_GetPathsByPattern_MultiLevel()
    {
        using var cache = new TreeMemoryCache();
        cache.SetTreeValue("A:B:C", 1);
        cache.SetTreeValue("A:B:D", 2);
        cache.SetTreeValue("A:E:F", 3);

        // A:B:* 模式应匹配 A:B:C 和 A:B:D
        var abPaths = cache.GetPathsByPattern("A:B:*").ToList();
        Assert.Equal(2, abPaths.Count);
        Assert.Contains("A:B:C", abPaths);
        Assert.Contains("A:B:D", abPaths);
    }

    [Fact]
    public void RemoveTree_ShouldInvalidateSegmentIndexLookups()
    {
        using var cache = new TreeMemoryCache();
        cache.SetTreeValue("A:1", 1);
        cache.SetTreeValue("A:2", 2);
        cache.SetTreeValue("B:1", 3);

        cache.RemoveTree("A:1");

        var aPaths = cache.GetPathsByPattern("A:*").ToList();
        Assert.Single(aPaths);
        Assert.Contains("A:2", aPaths);
    }

    [Fact]
    public void RemoveByTag_ShouldInvalidateSegmentIndexLookups()
    {
        using var cache = new TreeMemoryCache();
        cache.SetTree("A:1", 1, "tag-x").Dispose();
        cache.SetTree("A:2", 2, "tag-y").Dispose();

        cache.RemoveByTag("tag-x");

        var aPaths = cache.GetPathsByPattern("A:*").ToList();
        Assert.Single(aPaths);
        Assert.Contains("A:2", aPaths);
    }

    [Fact]
    public void SetTree_OverwriteSamePath_ShouldNotDuplicateInIndex()
    {
        using var cache = new TreeMemoryCache();
        cache.SetTreeValue("A:B", 1);
        cache.SetTreeValue("A:B", 2);
        cache.SetTreeValue("A:B", 3);

        var abPaths = cache.GetPathsByPattern("A:*").ToList();
        Assert.Single(abPaths);
        Assert.Equal("A:B", abPaths[0]);
    }

    [Fact]
    public void GetPathsByPattern_WildcardAll_ShouldReturnAllPaths()
    {
        using var cache = new TreeMemoryCache();
        cache.SetTreeValue("A:1", 1);
        cache.SetTreeValue("B:2", 2);
        cache.SetTreeValue("C:3", 3);

        var all = cache.GetPathsByPattern("*").ToList();
        // 包含中间节点(A、B、C)和目标节点(A:1、B:2、C:3)
        Assert.Contains("A:1", all);
        Assert.Contains("B:2", all);
        Assert.Contains("C:3", all);
    }
}