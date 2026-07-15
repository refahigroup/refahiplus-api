using Refahi.Modules.Store.Application.Contracts.Dtos.Products.V2;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.Store.Application.Services;

internal static class SyntheticOfferDtoMapper
{
    public static SyntheticOfferDto MapOffer(
        SyntheticOfferReadModel row,
        AgreementProductDto agreementProduct,
        IPathService pathService)
    {
        var discountPercent = row.DiscountedPriceMinor.HasValue
            ? (int?)Math.Round(
                (row.OriginalPriceMinor - row.DiscountedPriceMinor.Value) * 100m / row.OriginalPriceMinor)
            : null;

        return new SyntheticOfferDto(
            row.OfferKey,
            row.OfferKind,
            row.ProductId,
            row.ProductTitle,
            row.ProductSlug,
            row.ShopId,
            row.ShopName,
            row.ShopSlug,
            row.VariantId,
            row.VariantLabel,
            row.SessionId,
            row.SessionDate,
            row.SessionStartTime?.ToString("HH:mm"),
            row.SessionEndTime?.ToString("HH:mm"),
            row.SessionTitle,
            row.OriginalPriceMinor,
            row.DiscountedPriceMinor,
            row.EffectivePriceMinor,
            discountPercent,
            row.AvailableStock,
            row.ConfiguredCapacity,
            row.RequiresUsageDate,
            row.FromDate,
            row.ToDate,
            row.OfferKind is "StockProduct" or "StockVariant" ? "StockSnapshot" : "AtCart",
            ((ProductType)agreementProduct.ProductType).ToString(),
            ((DeliveryType)agreementProduct.DeliveryType).ToString(),
            ((SalesModel)agreementProduct.SalesModel).ToString(),
            agreementProduct.CategoryId,
            agreementProduct.CategoryName,
            row.ImageUrl is null ? null : pathService.MakeAbsoluteMediaUrl(row.ImageUrl),
            new SyntheticOfferPurchaseSelectionDto(
                row.ShopId,
                row.ProductId,
                row.VariantId,
                row.SessionId,
                row.FixedUsageDate));
    }
}
