namespace Refahi.Modules.Commerce.Application.Contracts.Providers;

public sealed record CommerceIssuedTicket(
    string Code, 
    bool IsChild
)
{
    public string? ProviderTicketId { get; init; }
    public string? Title { get; init; }
    public string? QrCode { get; init; }
    public string? GroupId { get; init; }
}
