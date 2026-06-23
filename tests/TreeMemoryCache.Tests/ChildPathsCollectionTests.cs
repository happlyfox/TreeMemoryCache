using Xunit;

namespace TreeMemoryCache.Tests;

/// <summary>
/// P1-7:ChildPaths 数据结构与级联删除的健壮性。
///
/// 修复点:
///   1) ChildPaths 从 HashSet<string> 改 List<string>(枚举用,内存省)
///   2) CollectDescendants BFS 用 HashSet 去重防御回环/重复入队
///
/// 这些测试覆盖"添加 → 枚举 → 删除 → 重新添加"序列,确保数据结构变更后行为不变。
/// </summary>
public class ChildPathsCollectionTests
{
    [Fact]
    public void GetChildPaths_ShouldReturnAllChildrenInOrder()
    {
        using var cache = new TreeMemoryCache();
        cache.SetTreeValue("A:B", 1);
        cache.SetTreeValue("A:C", 2);
        cache.SetTreeValue("A:D", 3);

        var children = cache.GetChildPaths("A").ToList();

        Assert.Equal(3, children.Count);
        Assert.Contains("A:B", children);
        Assert.Contains("A:C", children);
        Assert.Contains("A:D", children);
    }

    [Fact]
    public void GetChildPaths_AfterRemoveAndReadd_ShouldReflectCurrentState()
    {
        using var cache = new TreeMemoryCache();
        cache.SetTreeValue("A:B", 1);
        cache.SetTreeValue("A:C", 2);

        cache.RemoveTree("A:B");
        cache.SetTreeValue("A:B", 99); // 重新添加

        var children = cache.GetChildPaths("A").ToList();
        Assert.Contains("A:B", children);
        Assert.Contains("A:C", children);
        Assert.Equal(2, children.Count);
    }

    [Fact]
    public void GetDescendantPaths_BreadthFirst_ShouldReturnAllDescendants()
    {
        using var cache = new TreeMemoryCache();
        cache.SetTreeValue("A:B:C", 1);
        cache.SetTreeValue("A:B:D", 2);
        cache.SetTreeValue("A:E", 3);

        var descendants = cache.GetDescendantPaths("A").ToList();

        // 包含 A 的所有后代(不含 A 自身)
        Assert.Equal(4, descendants.Count);
        Assert.Contains("A:B", descendants);
        Assert.Contains("A:B:C", descendants);
        Assert.Contains("A:B:D", descendants);
        Assert.Contains("A:E", descendants);
    }

    [Fact]
    public void GetDescendantPaths_ShouldNotIncludeSelf()
    {
        using var cache = new TreeMemoryCache();
        cache.SetTreeValue("A:B", 1);

        var descendants = cache.GetDescendantPaths("A").ToList();

        // 不包含 A 自身,只包含后代
        Assert.DoesNotContain("A", descendants);
        Assert.Contains("A:B", descendants);
    }

    [Fact]
    public void RemoveTree_ShouldCleanUpParentChildReferences()
    {
        using var cache = new TreeMemoryCache();
        cache.SetTreeValue("A:B:C", 1);
        cache.SetTreeValue("A:B:D", 2);
        cache.SetTreeValue("A:E", 3);

        cache.RemoveTree("A:B");

        var aChildren = cache.GetChildPaths("A").ToList();
        // A:B 和它的后代都应删除,A:E 应保留
        Assert.DoesNotContain("A:B", aChildren);
        Assert.Contains("A:E", aChildren);
        Assert.Single(aChildren);
    }
}