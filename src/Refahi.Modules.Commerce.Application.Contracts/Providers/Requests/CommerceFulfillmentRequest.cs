namespace Refahi.Modules.Commerce.Application.Contracts.Providers.Requests;

public sealed record CommerceFulfillmentRequest(
    string OperationId, 
    string RecipientName, 
    string RecipientMobile,
    IReadOnlyList<CommerceFulfillmentLine> Lines
)
{
    public string? ReservationReference { get; init; }
    public string? ReservationContext { get; init; }
}
