using Microsoft.Extensions.Caching.Memory;
using TreeMemoryCache.Persistence;
using Xunit;

namespace TreeMemoryCache.Tests;

public class PersistenceTests
{
    [Fact]
    public async Task JsonPersistence_SaveAndLoad_ShouldRestoreData()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var persistence = new JsonFilePersistence(tempFile);

            // 创建并保存
            using (var cache = new TreeMemoryCache(persistence))
            {
                cache.SetTreeValue("A:B:C", "test-value");
                await cache.SaveAsync();
            }

            // 重新加载
            using (var cache2 = new TreeMemoryCache(persistence))
            {
                await cache2.LoadAsync();

                Assert.True(cache2.TryGetTree<string>("A:B:C", out var value));
                Assert.Equal("test-value", value);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task Load_ShouldRestoreTreeStructure()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var persistence = new JsonFilePersistence(tempFile);

            // 创建并保存
            using (var cache = new TreeMemoryCache(persistence))
            {
                cache.SetTreeValue("root:child1:leaf", "v1");
                cache.SetTreeValue("root:child2", "v2");
                await cache.SaveAsync();
            }

            // 重新加载并验证树结构
            using (var cache2 = new TreeMemoryCache(persistence))
            {
                await cache2.LoadAsync();

                Assert.True(cache2.TryGetTree<string>("root:child1:leaf", out _));
                Assert.True(cache2.TryGetTree<string>("root:child2", out _));

                var children = cache2.GetChildPaths("root").ToList();
                Assert.Equal(2, children.Count);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task Load_ShouldRestoreTags()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var persistence = new JsonFilePersistence(tempFile);

            using (var cache = new TreeMemoryCache(persistence))
            {
                cache.SetTreeValue("data:item1", "value1", "my-tag");
                cache.SetTreeValue("data:item2", "value2", "my-tag");
                await cache.SaveAsync();
            }

            using (var cache2 = new TreeMemoryCache(persistence))
            {
                await cache2.LoadAsync();

                var taggedPaths = cache2.GetPathsByTag("my-tag").ToList();
                Assert.Equal(2, taggedPaths.Count);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void Exists_WhenFileNotExists_ShouldReturnFalse()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
        try
        {
            var persistence = new JsonFilePersistence(tempFile);
            Assert.False(persistence.Exists());
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void Exists_WhenFileExists_ShouldReturnTrue()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var persistence = new JsonFilePersistence(tempFile);
            File.WriteAllText(tempFile, "{}");
            Assert.True(persistence.Exists());
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task Load_ShouldSkipExpiredNodes()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var persistence = new JsonFilePersistence(tempFile);

            // 创建并保存一个已过期的节点
            using (var cache = new TreeMemoryCache(persistence))
            {
                var expiredTime = DateTimeOffset.UtcNow.AddDays(-1);  // 昨天过期
                var options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpiration = expiredTime
                };
                cache.SetTreeValue("expired:key", "value", options: options);
                cache.SetTreeValue("valid:key", "valid-value");  // 有效节点
                await cache.SaveAsync();
            }

            // 加载时已过期节点应被跳过
            using (var cache2 = new TreeMemoryCache(persistence))
            {
                await cache2.LoadAsync();

                Assert.False(cache2.TryGetTree<string>("expired:key", out _));
                Assert.True(cache2.TryGetTree<string>("valid:key", out var validValue));
                Assert.Equal("valid-value", validValue);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task Load_ShouldRestoreNodesWithoutExpiration()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var persistence = new JsonFilePersistence(tempFile);

            using (var cache = new TreeMemoryCache(persistence))
            {
                cache.SetTreeValue("no-expiry:key", "value");  // 无过期时间
                await cache.SaveAsync();
            }

            using (var cache2 = new TreeMemoryCache(persistence))
            {
                await cache2.LoadAsync();

                Assert.True(cache2.TryGetTree<string>("no-expiry:key", out var value));
                Assert.Equal("value", value);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task Load_ShouldRestoreChildOfExpiredNode_AsOrphan()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var persistence = new JsonFilePersistence(tempFile);

            using (var cache = new TreeMemoryCache(persistence))
            {
                // 父节点已过期
                var expiredTime = DateTimeOffset.UtcNow.AddDays(-1);
                var parentOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpiration = expiredTime
                };
                cache.SetTreeValue("parent:child", "child-value", options: parentOptions);

                // 子节点未过期
                cache.SetTreeValue("parent:child:grandchild", "grandchild-value");
                await cache.SaveAsync();
            }

            using (var cache2 = new TreeMemoryCache(persistence))
            {
                await cache2.LoadAsync();

                // 父节点被跳过，子节点成为孤儿但仍可访问
                Assert.False(cache2.TryGetTree<string>("parent:child", out _));
                Assert.True(cache2.TryGetTree<string>("parent:child:grandchild", out var value));
                Assert.Equal("grandchild-value", value);

                // 诊断显示孤儿节点
                var diagnostics = cache2.GetDiagnostics();
                Assert.True(diagnostics.DeadParentLinks > 0);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task Load_ShouldNotAddTagIndexForExpiredNode()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var persistence = new JsonFilePersistence(tempFile);

            using (var cache = new TreeMemoryCache(persistence))
            {
                // 已过期节点带标签
                var expiredTime = DateTimeOffset.UtcNow.AddDays(-1);
                var options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpiration = expiredTime
                };
                cache.SetTreeValue("expired:tagged", "value", "my-tag", options);

                // 有效节点带相同标签
                cache.SetTreeValue("valid:tagged", "value2", "my-tag");
                await cache.SaveAsync();
            }

            using (var cache2 = new TreeMemoryCache(persistence))
            {
                await cache2.LoadAsync();

                // 只应找到有效节点
                var taggedPaths = cache2.GetPathsByTag("my-tag").ToList();
                Assert.Single(taggedPaths);
                Assert.Contains("valid:tagged", taggedPaths);
                Assert.DoesNotContain("expired:tagged", taggedPaths);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
