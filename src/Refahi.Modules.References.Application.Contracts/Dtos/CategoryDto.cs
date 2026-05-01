namespace Refahi.Modules.References.Application.Contracts.Dtos;

public sealed record CategoryDto(
    int Id,
    string Name,
    string Slug,
    string CategoryCode,
    string? ImageUrl,
    int? ParentId,
    int SortOrder,
    bool IsActive,
    List<CategoryDto>? Children = null);
