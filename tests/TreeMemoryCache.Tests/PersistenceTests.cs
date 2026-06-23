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
}
