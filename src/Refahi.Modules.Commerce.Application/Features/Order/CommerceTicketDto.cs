namespace Refahi.Modules.Commerce.Application.Features.Order;

public sealed record CommerceTicketDto(Guid Id, bool IsChild, string? Code)
{
    public string? Title { get; init; }
    public string? QrCode { get; init; }
}
