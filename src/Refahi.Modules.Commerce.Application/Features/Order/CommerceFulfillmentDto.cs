namespace Refahi.Modules.Commerce.Application.Features.Order;

public sealed record CommerceFulfillmentDto(Guid Id, string ProviderKey, string Status, string? ProviderOrderCode,
    string? FailureReason, IReadOnlyList<CommerceTicketDto> Tickets)
{
    public string? ProviderInvoiceId { get; init; }
    public IReadOnlyList<Refahi.Modules.Commerce.Application.Contracts.Providers.CommerceDeliveryDocument> Documents { get; init; } = [];
}
