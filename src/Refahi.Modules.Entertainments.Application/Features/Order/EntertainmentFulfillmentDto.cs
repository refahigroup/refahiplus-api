namespace Refahi.Modules.Entertainments.Application.Features.Order;

public sealed record EntertainmentFulfillmentDto(Guid Id, string ProviderKey, string Status, string? ProviderOrderCode,
    string? FailureReason, IReadOnlyList<EntertainmentTicketDto> Tickets)
{
    public string? ProviderInvoiceId { get; init; }
    public IReadOnlyList<Refahi.Modules.Entertainments.Application.Contracts.Providers.EntertainmentDeliveryDocument> Documents { get; init; } = [];
}
