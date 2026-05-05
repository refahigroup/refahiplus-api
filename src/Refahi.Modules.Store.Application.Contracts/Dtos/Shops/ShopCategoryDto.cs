namespace Refahi.Modules.Store.Application.Contracts.Dtos.Shops;

public sealed record ShopCategoryDto(
    int Id,
    string Name,
    string Slug,
    string? ImageUrl,
    int? ParentId);
