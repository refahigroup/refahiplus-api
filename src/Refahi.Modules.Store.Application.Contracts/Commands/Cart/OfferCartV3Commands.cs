using MediatR;
using Refahi.Modules.Store.Application.Contracts.Queries.Cart;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Cart;

public sealed record UpdateOfferCartItemCommand(
    Guid UserId,
    int ModuleId,
    Guid CartItemId,
    int Quantity,
    bool AcceptOfferChanges = false
) : IRequest<OfferCartDto>;

public sealed record RemoveOfferCartItemCommand(Guid UserId, int ModuleId, Guid CartItemId)
    : IRequest<OfferCartDto>;

public sealed record ReconfirmOfferCartCommand(Guid UserId, int ModuleId) : IRequest<OfferCartDto>;

public sealed record SyncOfferCartCommand(
    Guid UserId,
    int ModuleId,
    string IdempotencyKey,
    IReadOnlyList<SyncOfferCartItemInput> Items,
    bool AcceptOfferChanges = false
) : IRequest<OfferCartDto>;

public sealed record SyncOfferCartItemInput(
    Guid OfferId,
    int Quantity,
    Guid? ProductVariantId = null,
    Guid? ProductSessionId = null,
    DateOnly? UsageDate = null,
    long SnapshotOriginalUnitPriceMinor = 0,
    long SnapshotFinalUnitPriceMinor = 0
);

public sealed record CartOfferChangedDetail(
    Guid? CartItemId,
    Guid RequestedOfferId,
    long SnapshotOriginalUnitPriceMinor,
    long SnapshotFinalUnitPriceMinor,
    Guid? CurrentOfferId,
    long? CurrentOriginalUnitPriceMinor,
    long? CurrentFinalUnitPriceMinor,
    string Reason
);

public sealed record CartOfferChangedConflictResponse(
    bool Success,
    string Code,
    string Message,
    IReadOnlyList<CartOfferChangedDetail> Details,
    int StatusCode
);

public sealed record CartIdempotencyConflictResponse(
    bool Success,
    string Code,
    string Message,
    int StatusCode
);
