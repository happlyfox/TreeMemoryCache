using Xunit;

namespace TreeMemoryCache.Tests;

/// <summary>
/// P0-4:确保"实际缓存条目数"与"树索引节点数"被正确区分。
///
/// 之前 EnsurePathExists 会创建只含路径、没缓存值的中间节点(如 A、A:B),
/// 但 GetStatistics().TotalNodeCount 统计的是 _nodes.Count,造成幽灵节点
/// 计入"节点总数"。修复后应区分 TotalCachedItems(真实缓存条目数)与
/// TotalTrackedNodes(树索引维护的节点数,含中间节点)。
/// </summary>
public class GhostNodeStatisticsTests
{
    [Fact]
    public void SetTree_SingleDeepPath_ShouldCountOneCachedItem()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTreeValue("A:B:C:D", 42);

        var stats = cache.GetStatistics();

        // 用户只调一次 SetTree,只有 1 个真实缓存条目
        Assert.Equal(1, stats.TotalCachedItems);
    }

    [Fact]
    public void SetTree_SingleDeepPath_TrackedNodesMayIncludeIntermediates()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTreeValue("A:B:C:D", 42);

        var stats = cache.GetStatistics();

        // 树索引至少维护 A、B:B、C:D 等中间节点(>=1,<=4)
        // 中间节点允许存在,但要明确是"tracked"而不是"cached"
        Assert.True(stats.TotalTrackedNodes >= 1,
            $"期望至少 1 个 tracked 节点(实际 {stats.TotalTrackedNodes})");
        Assert.True(stats.TotalTrackedNodes >= stats.TotalCachedItems,
            $"tracked 节点数应 >= cached 节点数(tracked={stats.TotalTrackedNodes}, cached={stats.TotalCachedItems})");
    }

    [Fact]
    public void SetTree_MultiplePathsAtDifferentDepths_CountsEachCachedItemOnce()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTreeValue("A", 1);
        cache.SetTreeValue("A:B", 2);
        cache.SetTreeValue("A:B:C", 3);

        var stats = cache.GetStatistics();

        // 3 次 SetTree 写入,3 个真实缓存条目
        Assert.Equal(3, stats.TotalCachedItems);
    }

    [Fact]
    public void SetTree_OverwriteSamePath_StillCountsAsOneCachedItem()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTreeValue("A:B", 1);
        cache.SetTreeValue("A:B", 2); // 覆盖
        cache.SetTreeValue("A:B", 3); // 覆盖

        var stats = cache.GetStatistics();

        Assert.Equal(1, stats.TotalCachedItems);
    }
}
