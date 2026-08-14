using Microsoft.EntityFrameworkCore;
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
    }
}
