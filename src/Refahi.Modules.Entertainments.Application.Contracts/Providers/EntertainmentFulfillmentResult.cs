namespace Refahi.Modules.Entertainments.Application.Contracts.Providers;

public sealed record EntertainmentFulfillmentResult(
    string ProviderOrderCode, 
    IReadOnlyList<EntertainmentIssuedTicket> Tickets
)
{
    public string Status { get; init; } = "Completed";
    public string? ProviderInvoiceId { get; init; }
    public string? ProviderPaymentId { get; init; }
    public IReadOnlyList<EntertainmentDeliveryDocument> Documents { get; init; } = [];
}
