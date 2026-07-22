using Microsoft.EntityFrameworkCore;
using Npgsql;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;
using Refahi.Modules.Store.Infrastructure.Repositories;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class ShopProductRepositoryPostgresTests
{
    private const string ConnectionVariable = "REFAHI_STORE_TEST_CONNECTION";

    [Fact]
    public async Task DisplayableProducts_GroupsOfferingsAndAppliesAvailabilityBeforePaging()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var connectionBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Contains("test", connectionBuilder.Database, StringComparison.OrdinalIgnoreCase);
        await ResetAndMigrateAsync(connectionString);

        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var context = new StoreDbContext(options);

        var firstShop = CreateActiveShop("فروشگاه اول", "first-shop");
        var secondShop = CreateActiveShop("فروشگاه دوم", "second-shop");

        var firstProduct = Product.Create(Guid.NewGuid(), "محصول مشترک", "shared-product", stockCount: 10);
        var firstVariant = firstProduct.AddVariant([], 10, 6_000, sku: "first");
        var secondVariant = firstProduct.AddVariant([], 10, 4_000, sku: "second");

        var secondProduct = Product.Create(Guid.NewGuid(), "محصول مستقل", "other-product", stockCount: 10);
        var otherVariant = secondProduct.AddVariant([], 10, 5_000);

        var unavailableProduct = Product.Create(Guid.NewGuid(), "ناموجود", "unavailable", stockCount: 1);
        var unavailableVariant = unavailableProduct.AddVariant([], 0, 1_000);

        var sessionProduct = Product.Create(Guid.NewGuid(), "خدمت ظرفیت‌محور", "capacity-session", stockCount: 1);
        var sessionVariant = sessionProduct.AddVariant(
            [], 0, 2_000, capacityType: VariantCapacityType.Unlimited, salesModel: SalesModel.SessionBased);
        var today = new DateOnly(2026, 7, 13);
        sessionProduct.AddSession(today.AddDays(-1), new TimeOnly(10, 0), new TimeOnly(11, 0), 5);

        context.AddRange(firstShop, secondShop, firstProduct, secondProduct, unavailableProduct, sessionProduct);
        await context.SaveChangesAsync();

        var firstShopProduct = ShopProduct.Create(firstShop.Id, firstProduct.Id, 6_000, 0);
        firstShopProduct.AddVariantOffering(firstVariant.Id, 6_000, 5_000, isActive: true);
        var cheaperShopProduct = ShopProduct.Create(secondShop.Id, firstProduct.Id, 4_000, 0);
        var cheapestOffering = cheaperShopProduct.AddVariantOffering(secondVariant.Id, 4_000, 2_500, isActive: true);
        var otherShopProduct = ShopProduct.Create(secondShop.Id, secondProduct.Id, 5_000, 0);
        otherShopProduct.AddVariantOffering(otherVariant.Id, 5_000, 4_000, isActive: true);
        var unavailableShopProduct = ShopProduct.Create(firstShop.Id, unavailableProduct.Id, 1_000, 0);
        unavailableShopProduct.AddVariantOffering(unavailableVariant.Id, 1_000, null, isActive: true);
        var sessionShopProduct = ShopProduct.Create(secondShop.Id, sessionProduct.Id, 2_000, 0);
        sessionShopProduct.AddVariantOffering(sessionVariant.Id, 2_000, null, isActive: true);

        context.AddRange(
            firstShopProduct,
            cheaperShopProduct,
            otherShopProduct,
            unavailableShopProduct,
            sessionShopProduct);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new ShopProductRepository(context);
        var stockIds = new[]
        {
            firstProduct.AgreementProductId,
            secondProduct.AgreementProductId,
            unavailableProduct.AgreementProductId
        };
        var sessionIds = new[] { sessionProduct.AgreementProductId };

        var (items, total) = await repository.GetDisplayableProductsAsync(
            stockIds, sessionIds, today, null, "newest", 1, 10);

        Assert.Equal(3, total);
        Assert.Equal(3, items.Count);
        var shared = Assert.Single(items, x => x.ProductId == firstProduct.Id);
        Assert.Equal(cheapestOffering.Id, shared.ShopProductVariantId);
        Assert.Equal(secondShop.Id, shared.ShopId);
        Assert.Equal(2_500, shared.DiscountedPriceMinor);
        Assert.DoesNotContain(items, x => x.ProductId == unavailableProduct.Id);
        Assert.Contains(items, x => x.ProductId == sessionProduct.Id);

        var (shopSearch, shopSearchTotal) = await repository.GetDisplayableProductsAsync(
            stockIds, sessionIds, today, "فروشگاه اول", "price-asc", 1, 10);
        var searched = Assert.Single(shopSearch);
        Assert.Equal(1, shopSearchTotal);
        Assert.Equal(firstShop.Id, searched.ShopId);

        var (pricePage, priceTotal) = await repository.GetDisplayableProductsAsync(
            stockIds, sessionIds, today, null, "price-asc", 1, 1);
        Assert.Equal(3, priceTotal);
        Assert.Equal(sessionProduct.Id, Assert.Single(pricePage).ProductId);
    }

    [Fact]
    public async Task EligibleAgreementProducts_MatchProductCatalogEligibility()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var connectionBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Contains("test", connectionBuilder.Database, StringComparison.OrdinalIgnoreCase);
        await ResetAndMigrateAsync(connectionString);

        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var context = new StoreDbContext(options);

        var activeShop = CreateActiveShop("فروشگاه فعال", "active-shop");
        var pendingShop = Shop.Create("فروشگاه غیرفعال", "pending-shop", ShopType.Online, Guid.NewGuid());
        var stockProduct = Product.Create(Guid.NewGuid(), "کالای موجود", "stock", stockCount: 3);
        var unavailableProduct = Product.Create(Guid.NewGuid(), "کالای ناموجود", "unavailable-stock", stockCount: 0);
        var inactiveShopProduct = Product.Create(Guid.NewGuid(), "کالای فروشگاه غیرفعال", "inactive-shop", stockCount: 3);
        var sessionProduct = Product.Create(Guid.NewGuid(), "سانس آینده", "future-session", stockCount: 0);
        var expiredSessionProduct = Product.Create(Guid.NewGuid(), "سانس گذشته", "expired-session", stockCount: 0);
        var today = new DateOnly(2026, 7, 22);
        sessionProduct.AddSession(today.AddDays(1), new TimeOnly(9, 0), new TimeOnly(10, 0), 5);
        expiredSessionProduct.AddSession(today.AddDays(-1), new TimeOnly(9, 0), new TimeOnly(10, 0), 5);

        context.AddRange(
            activeShop, pendingShop, stockProduct, unavailableProduct,
            inactiveShopProduct, sessionProduct, expiredSessionProduct);
        await context.SaveChangesAsync();

        context.AddRange(
            ShopProduct.Create(activeShop.Id, stockProduct.Id, 10_000, 0),
            ShopProduct.Create(activeShop.Id, unavailableProduct.Id, 10_000, 0),
            ShopProduct.Create(pendingShop.Id, inactiveShopProduct.Id, 10_000, 0),
            ShopProduct.Create(activeShop.Id, sessionProduct.Id, 20_000, 0),
            ShopProduct.Create(activeShop.Id, expiredSessionProduct.Id, 20_000, 0));
        await context.SaveChangesAsync();

        var stockIds = new[]
        {
            stockProduct.AgreementProductId,
            unavailableProduct.AgreementProductId,
            inactiveShopProduct.AgreementProductId
        };
        var sessionIds = new[]
        {
            sessionProduct.AgreementProductId,
            expiredSessionProduct.AgreementProductId
        };
        var spec = new SyntheticOfferQuerySpec(
            stockIds,
            sessionIds,
            today,
            PageSize: 30,
            CurrentTime: new TimeOnly(12, 0));
        var repository = new SyntheticOfferReadRepository(connectionString);

        var eligibleIds = await repository.GetEligibleAgreementProductIdsAsync(spec);
        var (catalog, _) = await repository.GetProductCatalogAsync(spec);

        Assert.Equal(
            catalog.Select(item => item.AgreementProductId).Distinct().Order(),
            eligibleIds.Order());
        Assert.Contains(stockProduct.AgreementProductId, eligibleIds);
        Assert.Contains(sessionProduct.AgreementProductId, eligibleIds);
        Assert.DoesNotContain(unavailableProduct.AgreementProductId, eligibleIds);
        Assert.DoesNotContain(inactiveShopProduct.AgreementProductId, eligibleIds);
        Assert.DoesNotContain(expiredSessionProduct.AgreementProductId, eligibleIds);
    }

    private static Shop CreateActiveShop(string name, string slug)
    {
        var shop = Shop.Create(name, slug, ShopType.Online, Guid.NewGuid());
        shop.Approve();
        return shop;
    }

    private static async Task ResetAndMigrateAsync(string connectionString)
    {
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "drop schema if exists store cascade; drop table if exists public.\"__EFMigrationsHistory\";";
            await command.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var context = new StoreDbContext(options);
        await context.Database.MigrateAsync();
    }
}
