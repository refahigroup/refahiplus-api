namespace Refahi.Modules.Commerce.Application.Features.Order;

public sealed record CommerceOrderDto(
    Guid Id,
    Guid? OrderId,
    string Status,
    long TotalAmountMinor,
    IReadOnlyList<CommerceOrderItemDto> Items,
    IReadOnlyList<CommerceFulfillmentDto> Fulfillments
)
{
    public bool CanCancel { get; init; }
    public DateTimeOffset? PayableUntil { get; init; }
}
