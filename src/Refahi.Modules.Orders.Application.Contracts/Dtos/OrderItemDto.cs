namespace Refahi.Modules.Orders.Application.Contracts.Dtos;

public sealed record OrderItemDto(
    Guid Id,
    string Title,
    long UnitPriceMinor,
    int Quantity,
    long FinalPriceMinor,
    string CategoryCode,
    string[]? Tags,
    string? MetadataJson);
