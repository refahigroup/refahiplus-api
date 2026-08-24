namespace Refahi.Modules.Commerce.Application.Features.Order;

public sealed record CommerceFulfillmentDto(Guid Id, string ProviderKey, string Status, string? ProviderOrderCode,
    string? FailureReason, IReadOnlyList<CommerceTicketDto> Tickets);
