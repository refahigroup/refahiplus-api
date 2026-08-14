using Refahi.Modules.Store.Application.Contracts.Commands.Checkout;
using Refahi.Modules.Store.Application.Contracts.Offers;
using Refahi.Modules.Store.Application.Contracts.Products;
using Refahi.Modules.Store.Application.Contracts.Queries.Cart;
using Refahi.Modules.Store.Application.Contracts.Vendor;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class StoreCanonicalContractShapeTests
{
    [Fact]
    public void Latest_response_contract_property_sets_are_frozen()
    {
        Shape<PublicProductCatalogItemDto>("CategoryId", "CreatedAt", "Description", "FulfillmentMethod",
            "HasSessions", "HasVariants", "MainImageUrl", "Price", "ProductId", "ProductType",
            "SalesModel", "Slug", "Title");
        Shape<PublicProductDetailDto>("Images", "Offers", "Price", "PricingAuthority", "Product",
            "SelectedOfferId", "Sessions", "Specifications", "VariantAttributes", "Variants");
        Shape<PublicOfferDto>("AvailabilityCode", "DiscountPercent", "EndDateUtc", "FinalPriceMinor",
            "Id", "IsAvailable", "OriginalPriceMinor", "ProductId", "ProductSessionId",
            "ProductVariantId", "Selection", "ShopId", "ShopName", "ShopSlug", "StartDateUtc",
            "UpdatedAt", "Version");
        Shape<ProductDto>("CategoryId", "CreatedAt", "Description", "EligibleSalesChannels",
            "FulfillmentMethod", "Id", "IsActive", "IsDeleted", "ProductType", "SalesModel",
            "Slug", "SupplierId", "Title", "UpdatedAt", "Version");
        Shape<ProductVariantStructureDto>("Capacity", "CapacityType", "Combinations", "FromDate",
            "Id", "ImageUrl", "IsAvailable", "RequiresUsageDate", "Sku", "StockCount", "ToDate");
        Shape<ProductSessionStructureDto>("Capacity", "Date", "EndTime", "Id", "IsActive",
            "IsAvailable", "IsCancelled", "RemainingCapacity", "SoldCount", "StartTime", "Title");
        Shape<OfferDto>("CreatedAt", "DiscountPercent", "EndDateUtc", "FinalPriceMinor", "Id",
            "IsActive", "IsDeleted", "OriginalPriceMinor", "ProductId", "ProductSessionId",
            "ProductVariantId", "ShopId", "StartDateUtc", "UpdatedAt", "Version");
        Shape<OfferCartDto>("CartId", "CurrentTotalMinor", "HasOfferChanged", "Items",
            "SnapshotTotalMinor", "TotalItems");
        Shape<PlaceStoreOrderResponse>("CheckoutDestination", "FinalAmountMinor", "OrderId",
            "OrderNumber", "Status", "StoreOrderId");
        Shape<InPersonProductDto>("AgreementCategoryTermId", "AgreementId", "AgreementProductId",
            "CategoryId", "CategoryName", "CommissionPercent", "ProductId", "Title");
    }

    private static void Shape<T>(params string[] expected)
    {
        var actual = typeof(T).GetProperties().Select(x => x.Name).Order().ToArray();
        Assert.Equal(expected.Order().ToArray(), actual);
    }
}
