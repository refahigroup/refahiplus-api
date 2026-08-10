using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Queries.Cart;

public sealed record GetOfferCartQuery(Guid UserId, int ModuleId) : IRequest<OfferCartDto?>;

public sealed record OfferCartDto(
    Guid CartId,
    IReadOnlyList<OfferCartItemDto> Items,
    long SnapshotTotalMinor,
    long CurrentTotalMinor,
    int TotalItems,
    bool HasOfferChanged
);

public sealed record OfferCartItemDto(
    Guid CartItemId,
    Guid OfferId,
    Guid ProductId,
    Guid ShopId,
    Guid? ProductVariantId,
    Guid? ProductSessionId,
    DateOnly? UsageDate,
    int Quantity,
    string ProductTitle,
    string ProductSlug,
    string? ProductImageUrl,
    string ShopName,
    string ShopSlug,
    string? ProductVariantLabel,
    string? ProductSessionLabel,
    string FulfillmentMethod,
    long SnapshotOriginalUnitPriceMinor,
    long SnapshotFinalUnitPriceMinor,
    Guid? CurrentOfferId,
    long? CurrentOriginalUnitPriceMinor,
    long? CurrentFinalUnitPriceMinor,
    bool HasOfferChanged,
    bool IsAvailable,
    string AvailabilityCode,
    string AvailabilityReason
);
