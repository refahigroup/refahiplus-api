using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;
using Refahi.Modules.Store.Domain.Enums;

namespace Refahi.Modules.Store.Application.Contracts.Products.V3;

public sealed record ProductV3Dto(
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

public sealed record CreateProductV3Command(
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
) : IRequest<ProductV3Dto>;

public sealed record UpdateProductV3Command(
    Guid ActorUserId,
    bool IsAdmin,
    Guid ProductId,
    string Title,
    string? Description
) : IRequest<ProductV3Dto>;

public sealed record SetProductV3ActivationCommand(
    Guid ActorUserId,
    bool IsAdmin,
    Guid ProductId,
    short EligibilityChannel,
    bool IsActive
) : IRequest<ProductV3Dto>;

public sealed record DeleteProductV3Command(Guid ActorUserId, bool IsAdmin, Guid ProductId)
    : IRequest<Unit>;

public sealed record GetProductV3Query(Guid ProductId, bool IncludeInactive)
    : IRequest<ProductV3Dto?>;

public sealed record ListProductsV3Query(
    Guid? SupplierId,
    int? CategoryId,
    bool IncludeInactive,
    int Page,
    int PageSize
) : IRequest<ProductV3Page>;

public sealed record ProductV3Page(
    IReadOnlyList<ProductV3Dto> Items,
    int Total,
    int Page,
    int PageSize
);

public sealed record ProductV3ManagementDetailDto(
    ProductV3Dto Product,
    IReadOnlyList<ProductImageDto> Images,
    IReadOnlyList<ProductVariantV3Dto> Variants,
    IReadOnlyList<VariantAttributeDto> VariantAttributes,
    IReadOnlyList<ProductSpecificationDto> Specifications,
    IReadOnlyList<ProductSessionV3Dto> Sessions,
    string PricingAuthority,
    string SubresourceBasePath
);

public sealed record GetProductV3ManagementDetailQuery(Guid ProductId, string Role = "vendor")
    : IRequest<ProductV3ManagementDetailDto?>;

public sealed record ProductVariantV3Dto(
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

public sealed record VariantCombinationV3Input(Guid AttributeId, Guid ValueId);

public sealed record CreateProductVariantV3Command(
    Guid ActorUserId,
    bool IsAdmin,
    Guid ProductId,
    IReadOnlyList<VariantCombinationV3Input> Combinations,
    string? ImageUrl,
    int StockCount,
    string? Sku,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    VariantCapacityType CapacityType = VariantCapacityType.Unlimited,
    int? Capacity = null
) : IRequest<ProductVariantV3Dto>;

public sealed record UpdateProductVariantV3Command(
    Guid ActorUserId,
    bool IsAdmin,
    Guid ProductId,
    Guid VariantId,
    IReadOnlyList<VariantCombinationV3Input> Combinations,
    string? ImageUrl,
    int StockCount,
    string? Sku,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    VariantCapacityType CapacityType = VariantCapacityType.Unlimited,
    int? Capacity = null
) : IRequest<ProductVariantV3Dto>;

public sealed record DeleteProductVariantV3Command(
    Guid ActorUserId,
    bool IsAdmin,
    Guid ProductId,
    Guid VariantId
) : IRequest<Unit>;

public sealed record ProductSessionV3Dto(
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

public sealed record CreateProductSessionV3Command(
    Guid ActorUserId,
    bool IsAdmin,
    Guid ProductId,
    string Date,
    string StartTime,
    string EndTime,
    int Capacity,
    string? Title
) : IRequest<ProductSessionV3Dto>;

public sealed record UpdateProductSessionV3Command(
    Guid ActorUserId,
    bool IsAdmin,
    Guid ProductId,
    Guid SessionId,
    int Capacity,
    string? Title,
    bool IsActive
) : IRequest<ProductSessionV3Dto>;
