using System.Data;
using Dapper;
using Refahi.Modules.Store.Domain.Repositories;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class SyntheticOfferReadModelMaterializationTests
{
    [Fact]
    public void Dapper_materializes_offer_projection_with_postgres_runtime_types()
    {
        var productId = Guid.NewGuid();
        var table = new DataTable();
        AddColumn<string>(table, "OfferKey");
        AddColumn<string>(table, "OfferKind");
        AddColumn<Guid>(table, "ProductId");
        AddColumn<Guid>(table, "AgreementProductId");
        AddColumn<string>(table, "ProductTitle");
        AddColumn<string>(table, "ProductSlug");
        AddColumn<DateTime>(table, "ProductCreatedAt");
        AddColumn<Guid>(table, "ShopId");
        AddColumn<string>(table, "ShopName");
        AddColumn<string>(table, "ShopSlug");
        AddColumn<Guid>(table, "VariantId");
        AddColumn<string>(table, "VariantLabel");
        AddColumn<Guid>(table, "SessionId");
        AddColumn<DateOnly>(table, "SessionDate");
        AddColumn<TimeOnly>(table, "SessionStartTime");
        AddColumn<TimeOnly>(table, "SessionEndTime");
        AddColumn<string>(table, "SessionTitle");
        AddColumn<long>(table, "OriginalPriceMinor");
        AddColumn<long>(table, "DiscountedPriceMinor");
        AddColumn<long>(table, "EffectivePriceMinor");
        AddColumn<int>(table, "AvailableStock");
        AddColumn<int>(table, "ConfiguredCapacity");
        AddColumn<bool>(table, "RequiresUsageDate");
        AddColumn<DateOnly>(table, "FromDate");
        AddColumn<DateOnly>(table, "ToDate");
        AddColumn<DateOnly>(table, "FixedUsageDate");
        AddColumn<string>(table, "ImageUrl");
        AddColumn<bool>(table, "HasVariants");
        AddColumn<bool>(table, "HasSessions");

        table.Rows.Add(
            $"sp:{Guid.NewGuid():N}", "StockProduct", productId, Guid.NewGuid(),
            "محصول تست", "test-product", DateTime.UtcNow, Guid.NewGuid(), "فروشگاه تست", "test-shop",
            DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value,
            200_000L, DBNull.Value, 200_000L, 5, DBNull.Value, false,
            DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, false, false);

        using var reader = table.CreateDataReader();
        var parser = reader.GetRowParser<SyntheticOfferReadModel>();
        Assert.True(reader.Read());

        var row = parser(reader);

        Assert.Equal(productId, row.ProductId);
        Assert.Equal("StockProduct", row.OfferKind);
        Assert.Null(row.VariantId);
        Assert.Null(row.SessionDate);
        Assert.Null(row.DiscountedPriceMinor);
        Assert.Equal(200_000L, row.EffectivePriceMinor);
    }

    private static void AddColumn<T>(DataTable table, string name)
        => table.Columns.Add(name, typeof(T));
}
