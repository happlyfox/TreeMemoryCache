using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace TreeMemoryCache.Tests;

/// <summary>
/// P0-2:EstimateSize 的语义应该是契约"调用方负责估算",不是默认给 100 字节。
///
/// 之前 EstimateSize 默认返回 100(对未知类型),导致:
///   - 用户 SetTree("foo", largeObject) 不传 Size,会被估算成 100 字节
///   - MemoryCacheOptions.SizeLimit 实际不生效
///   - CacheNode.Size 严重低估
///
/// 修复后:
///   - 默认 Size=0(对未知类型,等同"不参与 Size 统计")
///   - 仍然提供可注入的 ISizeEstimator 给想自定义估算的调用方
/// </summary>
public class SizeEstimatorContractTests
{
    [Fact]
    public void SetTree_WithoutSizeOption_ShouldDefaultToZeroSize()
    {
        using var cache = new TreeMemoryCache();
        cache.SetTreeValue("A:B", new { X = 1, Y = 2, Z = 3 });

        var stats = cache.GetStatistics();

        // 之前会算成 100(默认),现在应该为 0
        Assert.Equal(0, stats.TotalCacheSize);
    }

    [Fact]
    public void SetTree_WithExplicitSize_ShouldHonorIt()
    {
        using var cache = new TreeMemoryCache();
        var entryOptions = new MemoryCacheEntryOptions { Size = 42 };
        cache.SetTree("A:B", "any", entryOptions).Dispose();

        var stats = cache.GetStatistics();

        Assert.Equal(42, stats.TotalCacheSize);
    }

    [Fact]
    public void SetTree_StringDefaultSize_ShouldMatchStringLength()
    {
        using var cache = new TreeMemoryCache();
        cache.SetTreeValue("A:B", "hello");

        var stats = cache.GetStatistics();

        // 字符串类型仍走精确估算:5 chars * 2 = 10
        Assert.Equal(10, stats.TotalCacheSize);
    }

    [Fact]
    public void SetTree_ByteArrayDefaultSize_ShouldMatchArrayLength()
    {
        using var cache = new TreeMemoryCache();
        var bytes = new byte[100];
        cache.SetTreeValue("A:B", bytes);

        var stats = cache.GetStatistics();

        // byte[] 走精确估算:length = 100
        Assert.Equal(100, stats.TotalCacheSize);
    }

    [Fact]
    public void CustomISizeEstimator_ShouldOverrideDefaultEstimation()
    {
        // 通过 ISizeEstimator 注入,实现可定制的 Size 估算
        using var cache = new TreeMemoryCache(sizeEstimator: new FixedSizeEstimator(7));

        cache.SetTreeValue("A:B", new object());

        var stats = cache.GetStatistics();

        Assert.Equal(7, stats.TotalCacheSize);
    }

    private sealed class FixedSizeEstimator : ISizeEstimator
    {
        private readonly long _size;
        public FixedSizeEstimator(long size) => _size = size;
        public long EstimateSize<T>(T value) => _size;
    }
}
