namespace Refahi.Modules.Commerce.Application.Features.Cart;

public sealed record CommerceCartDto(
    Guid CartId, 
    IReadOnlyList<CommerceCartItemDto> Items, 
    long TotalAmountMinor
);

