namespace Refahi.Modules.References.Application.Contracts.Dtos;

public sealed record ProvinceDto(
    int Id,
    string Name,
    string NameEn,
    string Slug,
    int SortOrder,
    bool IsActive);
