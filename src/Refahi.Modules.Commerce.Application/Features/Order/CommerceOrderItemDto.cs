namespace Refahi.Modules.Commerce.Application.Features.Order;

public sealed record CommerceOrderItemDto(Guid Id, string ProviderKey, string SellerKey, string Title, string OfferTitle,
    string PurchaseOptionKey, int Quantity, long UnitPriceMinor, string CategoryCode);
