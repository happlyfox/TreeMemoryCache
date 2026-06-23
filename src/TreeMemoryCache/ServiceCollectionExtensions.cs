using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TreeMemoryCache;

/// <summary>
/// TreeMemoryCache 的依赖注入扩展方法,用于在 ASP.NET Core / .NET 主机中注册服务。
/// </summary>
public static class TreeMemoryCacheServiceCollectionExtensions
{
    /// <summary>
    /// 注册 <see cref="ITreeMemoryCache"/> 为单例服务,同时兼容 <see cref="IMemoryCache"/> 接口。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="setupAction">可选的 <see cref="MemoryCacheOptions"/> 配置委托。</param>
    /// <returns>原 <see cref="IServiceCollection"/>,便于链式调用。</returns>
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
            return new TreeMemoryCache(persistence: null, options, logger);
        });

        // 同时注册 IMemoryCache 接口，指向同一个实例，保持兼容性
        services.AddSingleton<IMemoryCache>(sp =>
            (IMemoryCache)sp.GetRequiredService<ITreeMemoryCache>());

        return services;
    }

    /// <summary>
    /// 注册 <see cref="ITreeMemoryCache"/>,支持在配置阶段使用 <see cref="IServiceProvider"/> 解析其他服务。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="setupAction">基于 <see cref="IServiceProvider"/> 的 <see cref="MemoryCacheOptions"/> 配置委托。</param>
    /// <returns>原 <see cref="IServiceCollection"/>,便于链式调用。</returns>
    public static IServiceCollection AddTreeMemoryCache(this IServiceCollection services, Action<IServiceProvider, MemoryCacheOptions> setupAction)
    {
        services.AddSingleton<ITreeMemoryCache>(sp =>
        {
            var options = new MemoryCacheOptions();
            setupAction(sp, options);
            var logger = sp.GetService<ILogger<TreeMemoryCache>>();
            return new TreeMemoryCache(persistence: null, options, logger);
        });

        // 同时注册 IMemoryCache 接口，指向同一个实例，保持兼容性
        services.AddSingleton<IMemoryCache>(sp =>
            (IMemoryCache)sp.GetRequiredService<ITreeMemoryCache>());

        return services;
    }
}
