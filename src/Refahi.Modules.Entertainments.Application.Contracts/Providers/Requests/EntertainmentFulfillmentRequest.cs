namespace Refahi.Modules.Entertainments.Application.Contracts.Providers.Requests;

public sealed record EntertainmentFulfillmentRequest(
    string OperationId, 
    string RecipientName, 
    string RecipientMobile,
    IReadOnlyList<EntertainmentFulfillmentLine> Lines
)
{
    public string? ReservationReference { get; init; }
    public string? ReservationContext { get; init; }
}
