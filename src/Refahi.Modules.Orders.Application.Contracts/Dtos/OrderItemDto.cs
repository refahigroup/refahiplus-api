namespace Refahi.Modules.Orders.Application.Contracts.Dtos;

public sealed record OrderItemDto(
    Guid Id,
    string Title,
    long UnitPriceMinor,
    int Quantity,
    long FinalPriceMinor,
    Guid SourceItemId,
    string CategoryCode,
    string[]? Tags,
    string? MetadataJson,
    short DeliveryMethod);
