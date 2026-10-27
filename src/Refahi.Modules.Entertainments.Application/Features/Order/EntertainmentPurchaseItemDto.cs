namespace Refahi.Modules.Entertainments.Application.Features.Order;

public sealed record EntertainmentPurchaseItemDto(Guid Id, string ProviderKey, string SupplierKey, string Title, string OfferTitle,
    string PurchaseOptionKey, int Quantity, long UnitPriceMinor, string CategoryCode);
