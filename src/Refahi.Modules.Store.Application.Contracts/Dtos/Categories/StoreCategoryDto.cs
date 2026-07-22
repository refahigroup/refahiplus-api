namespace Refahi.Modules.Store.Application.Contracts.Dtos.Categories;

public sealed record StoreCategoryDto(
    int Id,
    string Name,
    string Slug,
    string CategoryCode,
    string? ImageUrl,
    int? ParentId,
    string? ParentTitle,
    int SortOrder,
    bool IsActive);
