namespace Refahi.Modules.Commerce.Application.Features.Order;

public sealed record CommerceTicketDto(Guid Id, bool IsChild, string? Code);
