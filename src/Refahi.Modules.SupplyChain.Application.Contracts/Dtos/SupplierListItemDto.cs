namespace Refahi.Modules.SupplyChain.Application.Contracts.Dtos;

public sealed record SupplierListItemDto(
    Guid Id,
    string DisplayName,
    short Type,
    string TypeName,
    short Status,
    string StatusName,
    int? CityId,
    DateTimeOffset CreatedAt);
