using Refahi.Shared.Services.Cache;
using StackExchange.Redis;

namespace Refahi.Api.Services.Chaching;

public static class CacheServiceDI
{
    public static IServiceCollection RegisterCachingService(this IServiceCollection services, IConfiguration configuration, bool IsDevelopment)
    {

        if (IsDevelopment)
        {
            services.AddMemoryCache();
            services.AddSingleton<ICacheService, InMemoryCacheService>();
        }
        else
        {
            services.AddSingleton<IConnectionMultiplexer>(
                _ => ConnectionMultiplexer.Connect(configuration["CacheService:Redis:Connection"])
            );

            services.AddSingleton<ICacheService, RedisCacheService>();
        }

        return services;
    }
}
