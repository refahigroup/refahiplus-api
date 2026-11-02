namespace Refahi.Modules.Entertainments.Application.Features.Order;

public sealed record EntertainmentTicketDto(Guid Id, bool IsChild, string? Code)
{
    public string? ProgramTitle { get; init; }
    public string? VenueTitle { get; init; }
    public string? Address { get; init; }
    public DateTimeOffset? StartsAt { get; init; }
    public string? Title { get; init; }
    public string? QrCode { get; init; }
}
