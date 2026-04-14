namespace Refahi.Modules.Store.Application.Contracts.Dtos.Categories;

public sealed record CategoryDto(
    int Id,
    string Name,
    string Slug,
    string CategoryCode,
    string? ImageUrl,
    int? ParentId,
    int SortOrder,
    bool IsActive);
