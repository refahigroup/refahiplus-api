namespace Refahi.Modules.Entertainments.Application.Features.Order;

public sealed record EntertainmentTicketDto(Guid Id, bool IsChild, string? Code)
{
    public string? Title { get; init; }
    public string? QrCode { get; init; }
}
