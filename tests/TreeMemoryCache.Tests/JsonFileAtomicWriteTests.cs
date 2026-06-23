using TreeMemoryCache.Persistence;
using Xunit;

namespace TreeMemoryCache.Tests;

/// <summary>
/// P1-8:JsonFilePersistence.SaveAsync 应当是原子的——不留下 .tmp 临时文件。
///
/// 之前的实现用 "File.Delete + File.Move" 序列,不是原子操作。
/// 修复后用 File.Move(temp, target, overwrite: true) 一次原子替换,
/// 即使在断电场景下也不会留下半写文件。
/// </summary>
public class JsonFileAtomicWriteTests
{
    [Fact]
    public async Task SaveAsync_ShouldNotLeaveTempFile()
    {
        var tempFile = Path.GetTempFileName();
        File.Delete(tempFile); // 让 SaveAsync 自己创建
        try
        {
            var persistence = new JsonFilePersistence(tempFile);

            using (var cache = new TreeMemoryCache(persistence))
            {
                cache.SetTreeValue("A:B", 1);
                await cache.SaveAsync();
            }

            // 目标文件应存在
            Assert.True(File.Exists(tempFile));

            // 不应残留 .tmp 临时文件
            var tempSibling = tempFile + ".tmp";
            Assert.False(File.Exists(tempSibling),
                $"SaveAsync 留下了临时文件: {tempSibling}");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
            var tempSibling = tempFile + ".tmp";
            if (File.Exists(tempSibling))
                File.Delete(tempSibling);
        }
    }

    [Fact]
    public async Task SaveAsync_OverwriteExistingFile_ShouldBeAtomic()
    {
        // 模拟"目标文件已存在"场景:之前用 File.Delete + File.Move 序列,
        // Delete 失败时 tmp 残留且 target 未更新。改用 File.Move(tmp, target, overwrite:true)
        // 一次原子替换,Delete 步骤不再需要。
        var tempFile = Path.GetTempFileName();
        File.Delete(tempFile);
        try
        {
            var persistence = new JsonFilePersistence(tempFile);

            // 第一次写入
            using (var cache = new TreeMemoryCache(persistence))
            {
                cache.SetTreeValue("A", 1);
                await cache.SaveAsync();
            }
            Assert.True(File.Exists(tempFile));

            // 第二次写入(覆盖已存在的文件)
            using (var cache = new TreeMemoryCache(persistence))
            {
                cache.SetTreeValue("B", 2);
                await cache.SaveAsync();
            }

            // 目标文件应仍是有效 JSON(覆盖成功)
            Assert.True(File.Exists(tempFile));
            var json = await File.ReadAllTextAsync(tempFile);
            Assert.Contains("B", json);

            // 不应残留 .tmp
            Assert.False(File.Exists(tempFile + ".tmp"));
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}