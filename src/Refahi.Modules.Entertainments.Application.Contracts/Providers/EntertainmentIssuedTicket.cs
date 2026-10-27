namespace Refahi.Modules.Entertainments.Application.Contracts.Providers;

public sealed record EntertainmentIssuedTicket(
    string Code, 
    bool IsChild
)
{
    public string? ProviderTicketId { get; init; }
    public string? Title { get; init; }
    public string? QrCode { get; init; }
    public string? GroupId { get; init; }
}
