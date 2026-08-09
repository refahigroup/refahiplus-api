using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;
using Refahi.Modules.Store.Infrastructure.Repositories;
using Refahi.Modules.Store.Infrastructure.Concurrency;
using Refahi.Shared.Extensions;
using Refahi.Shared.Infrastructure;

namespace Refahi.Modules.Store.Infrastructure;

public static class DI
{
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration, bool isDevelopment = false)
    {
        services.AddDbContext<StoreDbContext>(options =>
        {
            string connectionString = configuration.GetConnectionString();

            options.UseNpgsql(connectionString);
        });

        services.AddScoped<IShopRepository, ShopRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOfferRepository, OfferRepository>();
        services.AddScoped<IPublicCatalogRepository, PublicCatalogRepository>();
        services.AddScoped<IShopProductRepository, ShopProductRepository>();
        services.AddScoped<ISyntheticOfferReadRepository>(_ =>
            new SyntheticOfferReadRepository(configuration.GetConnectionString()));
        services.AddScoped<IProductSessionRepository, ProductSessionRepository>();
        services.AddScoped<IBannerRepository, BannerRepository>();
        services.AddScoped<IDailyDealRepository, DailyDealRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<IStoreOrderRepository, StoreOrderRepository>();
        services.AddScoped<IVoucherRepository, VoucherRepository>();
        services.AddScoped<IVoucherRefundOverrideRepository, VoucherRefundOverrideRepository>();
        services.AddSingleton<IStoreOrderMutationLock>(_ =>
            new PostgresStoreOrderMutationLock(configuration.GetConnectionString()));
        services.AddScoped<IStoreModuleRepository, StoreModuleRepository>();

        return services;
    }

    public static void UseInfrastructure(this IServiceProvider provider, bool IsDevelopment)
    {
        //if (!IsDevelopment)
        //    return;

        using var scope = provider.CreateScope();
        var tools = scope.ServiceProvider.GetRequiredService<IDbTools>();

        tools.ApplyMigrations<StoreDbContext>();
    }
}
