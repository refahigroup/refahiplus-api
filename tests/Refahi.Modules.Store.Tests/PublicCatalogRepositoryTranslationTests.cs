using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;
using Refahi.Modules.Store.Infrastructure.Repositories;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class PublicCatalogRepositoryTranslationTests
{
    [Fact]
    public void Eligibility_coordinates_query_with_category_ids_is_translatable_by_npgsql()
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=test;Password=test")
            .Options;
        using var db = new StoreDbContext(options);
        var repository = new PublicCatalogRepository(db);

        var query = repository.BuildEligibilityCoordinatesQuery(
            [11, 12],
            null,
            null,
            null,
            null,
            DateTimeOffset.UtcNow);

        var sql = query.Distinct().ToQueryString();

        Assert.Contains("CategoryId", sql);
        Assert.Contains("SupplierId", sql);
        Assert.Contains("ShopType", sql);
        Assert.Contains("IN (1, 2)", sql);
    }

    [Fact]
    public void Eligible_candidates_query_for_both_channels_is_translatable_by_npgsql()
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=test;Password=test")
            .Options;
        using var db = new StoreDbContext(options);
        var repository = new PublicCatalogRepository(db);
        var supplierId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var query = repository.BuildEligibleCandidatesQuery(
            [4],
            [
                new PublicCatalogEligibilityCoordinate(supplierId, 4, SalesChannel.Online),
                new PublicCatalogEligibilityCoordinate(supplierId, 4, SalesChannel.InPerson)
            ],
            null,
            null,
            null,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            [productId]);

        var sql = query.ToQueryString();

        Assert.Contains("ShopType", sql);
        Assert.Contains("UNION ALL", sql);
        Assert.Contains(productId.ToString(), sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Eligible_product_page_query_for_both_channels_is_translatable_by_npgsql()
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=test;Password=test")
            .Options;
        using var db = new StoreDbContext(options);
        var repository = new PublicCatalogRepository(db);
        var supplierId = Guid.NewGuid();

        var query = repository.BuildEligibleProductIdsQuery(
            [4],
            [
                new PublicCatalogEligibilityCoordinate(supplierId, 4, SalesChannel.Online),
                new PublicCatalogEligibilityCoordinate(supplierId, 4, SalesChannel.InPerson)
            ],
            null,
            null,
            null,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            "newest");

        var sql = query.Skip(0).Take(20).ToQueryString();

        Assert.Contains("GROUP BY", sql);
        Assert.Contains("UNION ALL", sql);
        Assert.Contains("LIMIT", sql);
    }

    [Fact]
    public void Product_detail_candidates_include_in_person_shops()
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=test;Password=test")
            .Options;
        using var db = new StoreDbContext(options);
        var repository = new PublicCatalogRepository(db);

        var query = repository.BuildEffectiveCandidatesQuery(
            moduleCategoryId: null,
            categoryId: null,
            shopId: null,
            shopSlug: "store-saeed",
            productSlug: "test-1",
            search: null,
            salesModel: null,
            atUtc: DateTimeOffset.UtcNow);

        var sql = query.ToQueryString();

        Assert.Contains("IN (1, 2)", sql);
        Assert.Contains("store-saeed", sql);
        Assert.Contains("test-1", sql);
    }
}
