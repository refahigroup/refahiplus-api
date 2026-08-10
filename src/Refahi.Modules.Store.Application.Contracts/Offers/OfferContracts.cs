using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Offers;

public sealed record OfferDto(Guid Id, Guid ProductId, Guid ShopId, Guid? ProductVariantId,
    Guid? ProductSessionId, long OriginalPriceMinor, decimal DiscountPercent, long FinalPriceMinor,
    DateTimeOffset StartDateUtc, DateTimeOffset? EndDateUtc, bool IsActive, bool IsDeleted,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, uint Version);
public sealed record CreateOfferCommand(Guid ActorUserId, bool IsAdmin, Guid ProductId, Guid ShopId,
    Guid? ProductVariantId, Guid? ProductSessionId, long OriginalPriceMinor, decimal DiscountPercent,
    DateTimeOffset StartDateUtc, DateTimeOffset? EndDateUtc) : IRequest<OfferDto>;
public sealed record UpdateOfferCommand(Guid ActorUserId, bool IsAdmin, Guid OfferId,
    long OriginalPriceMinor, decimal DiscountPercent, DateTimeOffset StartDateUtc,
    DateTimeOffset? EndDateUtc, uint ExpectedVersion) : IRequest<OfferDto>;
public sealed record SetOfferActivationCommand(Guid ActorUserId, bool IsAdmin, Guid OfferId,
    bool IsActive, uint ExpectedVersion) : IRequest<OfferDto>;
public sealed record DeleteOfferCommand(Guid ActorUserId, bool IsAdmin, Guid OfferId,
    uint ExpectedVersion) : IRequest<Unit>;
public sealed record GetOfferQuery(Guid OfferId, bool IncludeDeleted, DateTimeOffset? EffectiveAtUtc = null) : IRequest<OfferDto?>;
public sealed record ListOffersQuery(Guid? ProductId, Guid? ShopId, bool IncludeDeleted,
    DateTimeOffset? EffectiveAtUtc, int Page, int PageSize) : IRequest<OfferPage>;
public sealed record ResolveOfferQuery(Guid ProductId, Guid ShopId, Guid? ProductVariantId,
    Guid? ProductSessionId, DateTimeOffset AtUtc) : IRequest<OfferDto?>;
public sealed record OfferPage(IReadOnlyList<OfferDto> Items, int Total, int Page, int PageSize);
