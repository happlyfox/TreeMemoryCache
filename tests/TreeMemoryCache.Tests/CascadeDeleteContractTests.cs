using Xunit;

namespace TreeMemoryCache.Tests;

/// <summary>
/// 验证 TreeMemoryCache 严格遵循"树形缓存"语义：
/// 删除父节点必然级联删除全部后代，不存在任何保留子节点的模式。
///
/// 这些测试充当回归防护：任何尝试重新引入"孤儿化"或"软删除"模式的
/// 改动都会被以下测试捕获。
/// </summary>
public class CascadeDeleteContractTests
{
    [Fact]
    public void RemoveTree_ShouldAlwaysDeleteAllDescendants()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:Upward:Stations", "A").Dispose();
        cache.SetTree("Line:6:Upward:Station2", "B").Dispose();
        cache.SetTree("Line:6:Downward:Stations", "C").Dispose();

        // 不传 options，使用默认配置（TreeRemoveOptions { IncludeSelf = true }）
        cache.RemoveTree("Line:6:Upward");

        // 自身被删除
        Assert.False(cache.TryGetTree<string>("Line:6:Upward", out _));

        // 所有后代被级联删除（不再有"保留为孤儿"的可能）
        Assert.False(cache.TryGetTree<string>("Line:6:Upward:Stations", out _));
        Assert.False(cache.TryGetTree<string>("Line:6:Upward:Station2", out _));

        // 兄弟子树不受影响
        Assert.True(cache.TryGetTree<string>("Line:6:Downward:Stations", out var c));
        Assert.Equal("C", c);

        // 树索引中不存在任何"ParentPath 指向 Line:6:Upward 的孤儿"
        var childrenOfLine6 = cache.GetChildPaths("Line:6").ToList();
        Assert.DoesNotContain("Line:6:Upward", childrenOfLine6);
        Assert.DoesNotContain("Line:6:Upward:Stations", childrenOfLine6);
    }

    [Fact]
    public void RemoveTree_WithIncludeSelfFalse_ShouldDeleteOnlyDescendants()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("A:B:C", 1).Dispose();
        cache.SetTree("A:B:D", 2).Dispose();

        // 记录 A:B 在 _innerCache 中是否存在(只关心树索引,值可有可无)
        var beforeChildren = cache.GetChildPaths("A:B").ToList();
        Assert.Contains("A:B:C", beforeChildren);
        Assert.Contains("A:B:D", beforeChildren);

        cache.RemoveTree("A:B", new TreeRemoveOptions { IncludeSelf = false });

        // 后代被删除
        Assert.False(cache.TryGetTree<int>("A:B:C", out _));
        Assert.False(cache.TryGetTree<int>("A:B:D", out _));

        // 后代不再出现在 A:B 的子节点列表中
        var afterChildren = cache.GetChildPaths("A:B").ToList();
        Assert.DoesNotContain("A:B:C", afterChildren);
        Assert.DoesNotContain("A:B:D", afterChildren);
    }

    [Fact]
    public void RemoveTree_OnLeafNode_ShouldNotCreateOrphanedSiblings()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTreeValue("A:B:C", 42);
        cache.SetTreeValue("A:B:D", 99);

        cache.RemoveTree("A:B:C");

        // 叶子节点删除不影响兄弟节点
        Assert.False(cache.TryGetTree<int>("A:B:C", out _));
        Assert.True(cache.TryGetTree<int>("A:B:D", out var d));
        Assert.Equal(99, d);

        // 兄弟节点仍正确挂在 A:B 之下,不存在"孤儿"
        var childrenOfB = cache.GetChildPaths("A:B").ToList();
        Assert.Contains("A:B:D", childrenOfB);
        Assert.DoesNotContain("A:B:C", childrenOfB);
    }
}
