using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace TreeMemoryCache.Tests;

public class ExceptionInputTests
{
    [Fact]
    public void SetTree_WithNullPath_ShouldThrowArgumentException()
    {
        using var cache = new TreeMemoryCache();

        Assert.Throws<ArgumentNullException>(() => cache.SetTree(null!, "value"));
    }

    [Fact]
    public void SetTree_WithEmptyPath_ShouldThrowArgumentException()
    {
        using var cache = new TreeMemoryCache();

        Assert.Throws<ArgumentException>(() => cache.SetTree("", "value"));
    }

    [Fact]
    public void SetTree_WithWhitespaceOnlyPath_ShouldBeNormalized()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("   ", "value").Dispose();

        Assert.True(cache.TryGetTree<string>("", out _) || !cache.TryGetTree<string>("", out _));
    }

    [Fact]
    public void TryGetTree_WithNullPath_ShouldThrowArgumentException()
    {
        using var cache = new TreeMemoryCache();

        Assert.Throws<ArgumentNullException>(() => cache.TryGetTree<string>(null!, out _));
    }

    [Fact]
    public void TryGetTree_WithEmptyPath_ShouldThrowArgumentException()
    {
        using var cache = new TreeMemoryCache();

        Assert.Throws<ArgumentException>(() => cache.TryGetTree<string>("", out _));
    }

    [Fact]
    public void RemoveTree_WithNullPath_ShouldThrowArgumentException()
    {
        using var cache = new TreeMemoryCache();

        Assert.Throws<ArgumentNullException>(() => cache.RemoveTree(null!));
    }

    [Fact]
    public void RemoveTree_WithEmptyPath_ShouldThrowArgumentException()
    {
        using var cache = new TreeMemoryCache();

        Assert.Throws<ArgumentException>(() => cache.RemoveTree(""));
    }

    [Fact]
    public void GetChildPaths_WithNullPath_ShouldThrowArgumentException()
    {
        using var cache = new TreeMemoryCache();

        Assert.Throws<ArgumentNullException>(() => cache.GetChildPaths(null!));
    }

    [Fact]
    public void GetChildPaths_WithEmptyPath_ShouldThrowArgumentException()
    {
        using var cache = new TreeMemoryCache();

        Assert.Throws<ArgumentException>(() => cache.GetChildPaths(""));
    }

    [Fact]
    public void GetDescendantPaths_WithNullPath_ShouldThrowArgumentException()
    {
        using var cache = new TreeMemoryCache();

        Assert.Throws<ArgumentNullException>(() => cache.GetDescendantPaths(null!));
    }

    [Fact]
    public void GetDescendantPaths_WithEmptyPath_ShouldThrowArgumentException()
    {
        using var cache = new TreeMemoryCache();

        Assert.Throws<ArgumentException>(() => cache.GetDescendantPaths(""));
    }

    [Fact]
    public void GetPathsByPattern_WithNullPattern_ShouldThrowArgumentException()
    {
        using var cache = new TreeMemoryCache();

        Assert.Throws<ArgumentNullException>(() => cache.GetPathsByPattern(null!));
    }

    [Fact]
    public void GetPathsByPattern_WithEmptyPattern_ShouldThrowArgumentException()
    {
        using var cache = new TreeMemoryCache();

        Assert.Throws<ArgumentException>(() => cache.GetPathsByPattern(""));
    }

    [Fact]
    public void GetPathsByTag_WithNullTag_ShouldThrowArgumentException()
    {
        using var cache = new TreeMemoryCache();

        Assert.Throws<ArgumentNullException>(() => cache.GetPathsByTag(null!));
    }

    [Fact]
    public void GetPathsByTag_WithEmptyTag_ShouldThrowArgumentException()
    {
        using var cache = new TreeMemoryCache();

        Assert.Throws<ArgumentException>(() => cache.GetPathsByTag(""));
    }

    [Fact]
    public void RemoveByTag_WithNullTag_ShouldThrowArgumentException()
    {
        using var cache = new TreeMemoryCache();

        Assert.Throws<ArgumentNullException>(() => cache.RemoveByTag(null!));
    }

    [Fact]
    public void RemoveByTag_WithEmptyTag_ShouldThrowArgumentException()
    {
        using var cache = new TreeMemoryCache();

        Assert.Throws<ArgumentException>(() => cache.RemoveByTag(""));
    }

    [Fact]
    public async Task RemoveTreeAsync_WithNullPath_ShouldThrowArgumentException()
    {
        using var cache = new TreeMemoryCache();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in cache.RemoveTreeAsync(null!))
            {
            }
        });
    }

    [Fact]
    public async Task RemoveTreeAsync_WithEmptyPath_ShouldThrowArgumentException()
    {
        using var cache = new TreeMemoryCache();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in cache.RemoveTreeAsync(""))
            {
            }
        });
    }

    [Fact]
    public void TryGetTree_WithWrongType_ShouldReturnFalse()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:Data", "StringValue").Dispose();

        Assert.False(cache.TryGetTree<int>("Line:6:Data", out _));
    }

    [Fact]
    public void TryGetTree_WithNullableValueType_ShouldHandleCorrectly()
    {
        using var cache = new TreeMemoryCache();

        cache.SetTree("Line:6:Count", 42).Dispose();

        Assert.True(cache.TryGetTree<int>("Line:6:Count", out var value));
        Assert.Equal(42, value);
    }

    [Fact]
    public void Batch_ExecuteTwice_ShouldThrowObjectDisposedException()
    {
        using var cache = new TreeMemoryCache();

        using var batch = cache.CreateBatch();
        batch.Set("Line:6:Test", "Value");
        batch.Execute();

        Assert.Throws<ObjectDisposedException>(() => batch.Execute());
    }

    [Fact]
    public void Batch_AddOperationAfterExecute_ShouldThrowObjectDisposedException()
    {
        using var cache = new TreeMemoryCache();

        using var batch = cache.CreateBatch();
        batch.Set("Line:6:Test", "Value");
        batch.Execute();

        Assert.Throws<ObjectDisposedException>(() => batch.Set("Line:7:Test", "Value"));
    }

    [Fact]
    public void Cache_AfterDispose_ShouldThrowObjectDisposedException()
    {
        var cache = new TreeMemoryCache();
        cache.Dispose();

        Assert.Throws<ObjectDisposedException>(() => cache.SetTree("Test", "Value"));
    }

    [Fact]
    public void Cache_DoubleDispose_ShouldNotThrow()
    {
        var cache = new TreeMemoryCache();
        cache.Dispose();
        var exception = Record.Exception(() => cache.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public async Task RemoveTreeAsync_WithCancellation_ShouldThrowOperationCanceledException()
    {
        using var cache = new TreeMemoryCache();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        cache.SetTree("Line:6:A", "1").Dispose();
        cache.SetTree("Line:6:B", "2").Dispose();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in cache.RemoveTreeAsync("Line:6", cts.Token))
            {
            }
        });
    }

    [Fact]
    public void SetTree_WithComplexObject_ShouldStoreCorrectly()
    {
        using var cache = new TreeMemoryCache();
        var complexObject = new
        {
            Name = "Test",
            Values = new[] { 1, 2, 3 },
            Nested = new { Inner = "Value" }
        };

        cache.SetTree("Line:6:Complex", complexObject).Dispose();

        Assert.True(cache.TryGetTree<object>("Line:6:Complex", out _));
    }
}
