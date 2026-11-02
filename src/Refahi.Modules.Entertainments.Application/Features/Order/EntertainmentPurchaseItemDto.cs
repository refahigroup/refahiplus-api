namespace Refahi.Modules.Entertainments.Application.Features.Order;

public sealed record EntertainmentPurchaseItemDto(Guid Id, string Title, string OfferTitle,
    string PurchaseOptionKey, int Quantity, long UnitPriceMinor, string CategoryCode);
