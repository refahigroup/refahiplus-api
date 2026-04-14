namespace Refahi.Modules.Store.Application.Contracts.Dtos.Cart;

public sealed record CartDto(
    Guid CartId,
    List<CartItemDto> Items,
    long TotalMinor,
    int TotalItems);

public sealed record CartItemDto(
    Guid Id,
    Guid ProductId,
    string ProductTitle,
    string? ProductImageUrl,
    Guid? VariantId,
    string? VariantLabel,
    Guid? SessionId,
    string? SessionLabel,
    int Quantity,
    long UnitPriceMinor,
    long TotalPriceMinor,
    bool IsAvailable);
