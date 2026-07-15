using Microsoft.EntityFrameworkCore;
using Npgsql;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;
using Refahi.Modules.Store.Infrastructure.Repositories;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class SyntheticOfferReadRepositoryPostgresTests
{
    private const string ConnectionVariable = "REFAHI_STORE_TEST_CONNECTION";

    [Fact]
    public async Task ReadModel_projects_all_legacy_offer_shapes_without_reading_sold_capacity()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var connectionBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Contains("test", connectionBuilder.Database, StringComparison.OrdinalIgnoreCase);
        await ResetAndMigrateAsync(connectionString);

        var options = new DbContextOptionsBuilder<StoreDbContext>().UseNpgsql(connectionString).Options;
        await using var context = new StoreDbContext(options);
        var today = new DateOnly(2026, 7, 15);
        var shop = CreateActiveShop();

        var simpleProduct = Product.Create(Guid.NewGuid(), "محصول ساده", "simple-stock", stockCount: 5);

        var variantProduct = Product.Create(Guid.NewGuid(), "لباس", "clothing", stockCount: 1);
        var red = variantProduct.AddVariant([], 3, 1_000, sku: "red");
        var blue = variantProduct.AddVariant([], 2, 2_000, sku: "blue");

        var sessionProduct = Product.Create(Guid.NewGuid(), "استخر", "pool", stockCount: 1);
        sessionProduct.AddSession(today, new TimeOnly(13, 0), new TimeOnly(15, 0), 20, "ظرفیت تکمیل", 100_000);
        sessionProduct.Sessions.Single().Sell(20);
        sessionProduct.AddSession(today, new TimeOnly(9, 0), new TimeOnly(11, 0), 20, "گذشته", 0);
        sessionProduct.AddSession(today.AddDays(1), new TimeOnly(9, 0), new TimeOnly(11, 0), 20, "تعطیل", 0);
        sessionProduct.Sessions.Last().Cancel();

        var datedProduct = Product.Create(Guid.NewGuid(), "بلیط سینما", "cinema", stockCount: 1);
        var datedVariant = datedProduct.AddVariant(
            [], 0, 300_000, sku: "evening",
            fromDate: today, toDate: today.AddDays(3),
            capacityType: VariantCapacityType.PerEligibleDay, capacity: 50,
            salesModel: SalesModel.SessionBased);

        context.AddRange(shop, simpleProduct, variantProduct, sessionProduct, datedProduct);
        await context.SaveChangesAsync();

        var simpleShopProduct = ShopProduct.Create(shop.Id, simpleProduct.Id, 500_000, 450_000);
        var variantShopProduct = ShopProduct.Create(shop.Id, variantProduct.Id, 999_999, 0);
        variantShopProduct.AddVariantOffering(red.Id, 1_000, null, isActive: true);
        variantShopProduct.AddVariantOffering(blue.Id, 2_000, 1_800, isActive: true);
        var sessionShopProduct = ShopProduct.Create(shop.Id, sessionProduct.Id, 200_000, 0);
        var datedShopProduct = ShopProduct.Create(shop.Id, datedProduct.Id, 300_000, 0);
        datedShopProduct.AddVariantOffering(datedVariant.Id, 300_000, null, isActive: true);
        context.AddRange(simpleShopProduct, variantShopProduct, sessionShopProduct, datedShopProduct);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new SyntheticOfferReadRepository(connectionString);
        var spec = new SyntheticOfferQuerySpec(
            [simpleProduct.AgreementProductId, variantProduct.AgreementProductId],
            [sessionProduct.AgreementProductId, datedProduct.AgreementProductId],
            today,
            PageSize: 30,
            CurrentTime: new TimeOnly(12, 0));

        var (offers, offerTotal) = await repository.GetOffersAsync(spec);
        Assert.Equal(5, offerTotal);
        Assert.Contains(offers, x => x.OfferKind == "StockProduct" && x.ProductId == simpleProduct.Id);
        Assert.Equal(2, offers.Count(x => x.OfferKind == "StockVariant" && x.ProductId == variantProduct.Id));
        var fullSession = Assert.Single(offers, x => x.OfferKind == "ProductSession");
        Assert.Equal(300_000, fullSession.EffectivePriceMinor);
        Assert.Equal(20, fullSession.ConfiguredCapacity);
        Assert.DoesNotContain(offers, x => x.SessionTitle == "گذشته");
        Assert.DoesNotContain(offers, x => x.SessionTitle == "تعطیل");
        var dated = Assert.Single(offers, x => x.OfferKind == "SessionVariant");
        Assert.True(dated.RequiresUsageDate);
        Assert.Null(dated.FixedUsageDate);
        Assert.DoesNotContain(offers, x => x.ProductId == variantProduct.Id && x.OfferKind == "StockProduct");

        var (catalog, catalogTotal) = await repository.GetProductCatalogAsync(spec);
        Assert.Equal(4, catalogTotal);
        var clothing = Assert.Single(catalog, x => x.ProductId == variantProduct.Id);
        Assert.Equal(1_000, clothing.MinEffectivePriceMinor);
        Assert.Equal(1_800, clothing.MaxEffectivePriceMinor);
        Assert.Equal(2, clothing.OfferCount);
    }

    private static Shop CreateActiveShop()
    {
        var shop = Shop.Create("فروشگاه تست", "synthetic-offer-shop", ShopType.Online, Guid.NewGuid());
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

        var options = new DbContextOptionsBuilder<StoreDbContext>().UseNpgsql(connectionString).Options;
        await using var context = new StoreDbContext(options);
        await context.Database.MigrateAsync();
    }
}
