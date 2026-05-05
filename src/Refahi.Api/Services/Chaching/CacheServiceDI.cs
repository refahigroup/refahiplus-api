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

            string type = configuration["CacheService:Redis:Type"]?.ToLower() ?? "";
            string redis = configuration["CacheService:Redis:Connection"] ?? "";

            if (type == "redis" && !string.IsNullOrEmpty(redis))
            {
                services.AddSingleton<IConnectionMultiplexer>(
                    _ => ConnectionMultiplexer.Connect(redis)
                );

                services.AddSingleton<ICacheService, RedisCacheService>();
            }
            else
            {
                services.AddMemoryCache();
                services.AddSingleton<ICacheService, InMemoryCacheService>();

            }

        }

        return services;
    }
}
