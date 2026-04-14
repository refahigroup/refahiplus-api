namespace Refahi.Modules.Store.Application.Contracts.Dtos.Products;

public sealed record ProductImageDto(int Id, string ImageUrl, bool IsMain, int SortOrder);
