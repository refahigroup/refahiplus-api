namespace Refahi.Modules.References.Application.Contracts.Dtos;

public sealed record ProvinceDto(
    int Id,
    string Name,
    string Slug,
    int SortOrder,
    bool IsActive);
