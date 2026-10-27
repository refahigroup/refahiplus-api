namespace Refahi.Modules.Entertainments.Application.Features.Order;

public sealed record EntertainmentPurchaseDto(
    Guid Id,
    Guid? OrderId,
    string Status,
    long TotalAmountMinor,
    IReadOnlyList<EntertainmentPurchaseItemDto> Items,
    IReadOnlyList<EntertainmentFulfillmentDto> Fulfillments
)
{
    public bool CanCancel { get; init; }
    public DateTimeOffset? PayableUntil { get; init; }
}
