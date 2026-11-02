namespace Refahi.Modules.Entertainments.Application.Features.Cart;

public sealed record EntertainmentCartDto(
    Guid CartId, 
    IReadOnlyList<EntertainmentCartItemDto> Items, 
    long TotalAmountMinor
)
{
    public IReadOnlyDictionary<string, string> CheckoutVersions { get; init; } = new Dictionary<string, string>();
}

