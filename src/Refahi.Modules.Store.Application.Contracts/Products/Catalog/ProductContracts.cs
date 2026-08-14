using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;
using Refahi.Modules.Store.Domain.Enums;

namespace Refahi.Modules.Store.Application.Contracts.Products;

public sealed record ProductDto(
    Guid Id,
    Guid SupplierId,
    int CategoryId,
    short ProductType,
    short SalesModel,
    short FulfillmentMethod,
    string Title,
    string Slug,
    string? Description,
    bool IsActive,
    bool IsDeleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version
)
{
    /// <summary>Current AgreementCategoryTerm channel flags: Online=1, InPerson=2.</summary>
    public short EligibleSalesChannels { get; init; }
}

public sealed record CreateCatalogProductCommand(
    Guid ActorUserId,
    bool IsAdmin,
    Guid SupplierId,
    int CategoryId,
    short EligibilityChannel,
    short ProductType,
    short SalesModel,
    short FulfillmentMethod,
    string Title,
    string Slug,
    string? Description
) : IRequest<ProductDto>;

public sealed record UpdateCatalogProductCommand(
    Guid ActorUserId,
    bool IsAdmin,
    Guid ProductId,
    string Title,
    string? Description
) : IRequest<ProductDto>;

public sealed record SetCatalogProductActivationCommand(
    Guid ActorUserId,
    bool IsAdmin,
    Guid ProductId,
    short EligibilityChannel,
    bool IsActive
) : IRequest<ProductDto>;

public sealed record DeleteCatalogProductCommand(Guid ActorUserId, bool IsAdmin, Guid ProductId)
    : IRequest<Unit>;

public sealed record GetCatalogProductQuery(Guid ProductId, bool IncludeInactive)
    : IRequest<ProductDto?>;

public sealed record ListCatalogProductsQuery(
    Guid? SupplierId,
    int? CategoryId,
    bool IncludeInactive,
    int Page,
    int PageSize
) : IRequest<ProductPage>;

public sealed record ProductPage(
    IReadOnlyList<ProductDto> Items,
    int Total,
    int Page,
    int PageSize
);

public sealed record ProductManagementDetailDto(
    ProductDto Product,
    IReadOnlyList<ProductImageDto> Images,
    IReadOnlyList<ProductVariantStructureDto> Variants,
    IReadOnlyList<VariantAttributeDto> VariantAttributes,
    IReadOnlyList<ProductSpecificationDto> Specifications,
    IReadOnlyList<ProductSessionStructureDto> Sessions,
    string PricingAuthority,
    string SubresourceBasePath
);

public sealed record GetProductManagementDetailQuery(Guid ProductId, string Role = "vendor")
    : IRequest<ProductManagementDetailDto?>;

public sealed record ProductVariantStructureDto(
    Guid Id,
    string? Sku,
    string? ImageUrl,
    int StockCount,
    DateOnly? FromDate,
    DateOnly? ToDate,
    VariantCapacityType CapacityType,
    int? Capacity,
    bool RequiresUsageDate,
    bool IsAvailable,
    IReadOnlyList<VariantCombinationDto> Combinations
);

public sealed record VariantCombinationDto(
    Guid AttributeId,
    string AttributeName,
    Guid ValueId,
    string Value
);

public sealed record VariantCombinationInput(Guid AttributeId, Guid ValueId);

public sealed record CreateCatalogProductVariantCommand(
    Guid ActorUserId,
    bool IsAdmin,
    Guid ProductId,
    IReadOnlyList<VariantCombinationInput> Combinations,
    string? ImageUrl,
    int StockCount,
    string? Sku,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    VariantCapacityType CapacityType = VariantCapacityType.Unlimited,
    int? Capacity = null
) : IRequest<ProductVariantStructureDto>;

public sealed record UpdateCatalogProductVariantCommand(
    Guid ActorUserId,
    bool IsAdmin,
    Guid ProductId,
    Guid VariantId,
    IReadOnlyList<VariantCombinationInput> Combinations,
    string? ImageUrl,
    int StockCount,
    string? Sku,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    VariantCapacityType CapacityType = VariantCapacityType.Unlimited,
    int? Capacity = null
) : IRequest<ProductVariantStructureDto>;

public sealed record DeleteCatalogProductVariantCommand(
    Guid ActorUserId,
    bool IsAdmin,
    Guid ProductId,
    Guid VariantId
) : IRequest<Unit>;

public sealed record ProductSessionStructureDto(
    Guid Id,
    string Date,
    string StartTime,
    string EndTime,
    string? Title,
    int Capacity,
    int SoldCount,
    int RemainingCapacity,
    bool IsActive,
    bool IsCancelled,
    bool IsAvailable
);

public sealed record CreateCatalogProductSessionCommand(
    Guid ActorUserId,
    bool IsAdmin,
    Guid ProductId,
    string Date,
    string StartTime,
    string EndTime,
    int Capacity,
    string? Title
) : IRequest<ProductSessionStructureDto>;

public sealed record UpdateCatalogProductSessionCommand(
    Guid ActorUserId,
    bool IsAdmin,
    Guid ProductId,
    Guid SessionId,
    int Capacity,
    string? Title,
    bool IsActive
) : IRequest<ProductSessionStructureDto>;
