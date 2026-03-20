using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TreeMemoryCache;

/// <summary>
/// TreeMemoryCache 的依赖注入扩展方法。
/// </summary>
public static class TreeMemoryCacheServiceCollectionExtensions
{
    /// <summary>
    /// 注册树形缓存服务，并可选配置 MemoryCacheOptions。
    /// </summary>
    public static IServiceCollection AddTreeMemoryCache(this IServiceCollection services, Action<MemoryCacheOptions>? setupAction = null)
    {
        if (setupAction != null)
        {
            services.Configure(setupAction);
        }

        services.AddSingleton<ITreeMemoryCache>(sp =>
        {
            var options = sp.GetService<MemoryCacheOptions>() ?? new MemoryCacheOptions();
            var logger = sp.GetService<ILogger<TreeMemoryCache>>();
            return new TreeMemoryCache(options, logger);
        });

        return services;
    }

    /// <summary>
    /// 注册树形缓存服务，并允许基于 IServiceProvider 动态配置。
    /// </summary>
    public static IServiceCollection AddTreeMemoryCache(this IServiceCollection services, Action<IServiceProvider, MemoryCacheOptions> setupAction)
    {
        services.AddSingleton<ITreeMemoryCache>(sp =>
        {
            var options = new MemoryCacheOptions();
            setupAction(sp, options);
            var logger = sp.GetService<ILogger<TreeMemoryCache>>();
            return new TreeMemoryCache(options, logger);
        });

        return services;
    }
}
