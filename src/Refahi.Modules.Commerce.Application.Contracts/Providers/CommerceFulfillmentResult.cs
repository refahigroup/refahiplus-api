namespace Refahi.Modules.Commerce.Application.Contracts.Providers;

public sealed record CommerceFulfillmentResult(
    string ProviderOrderCode, 
    IReadOnlyList<CommerceIssuedTicket> Tickets
)
{
    public string Status { get; init; } = "Completed";
    public string? ProviderInvoiceId { get; init; }
    public string? ProviderPaymentId { get; init; }
    public IReadOnlyList<CommerceDeliveryDocument> Documents { get; init; } = [];
}
