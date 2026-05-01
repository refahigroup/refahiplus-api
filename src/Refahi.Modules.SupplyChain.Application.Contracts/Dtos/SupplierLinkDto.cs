namespace Refahi.Modules.SupplyChain.Application.Contracts.Dtos;

public sealed record SupplierLinkDto(
    Guid Id,
    short Type,
    string TypeName,
    string Url,
    string? Label,
    DateTimeOffset CreatedAt);
