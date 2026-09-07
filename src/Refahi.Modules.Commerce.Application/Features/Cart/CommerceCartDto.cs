namespace Refahi.Modules.Commerce.Application.Features.Cart;

public sealed record CommerceCartDto(
    Guid CartId, 
    IReadOnlyList<CommerceCartItemDto> Items, 
    long TotalAmountMinor
)
{
    public IReadOnlyDictionary<string, string> CheckoutVersions { get; init; } = new Dictionary<string, string>();
}

