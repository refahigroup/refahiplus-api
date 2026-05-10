using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Cart;

public sealed record SyncCartCommand(
    Guid UserId,
    int ModuleId,
    IReadOnlyList<SyncCartItemInput> Items,
    string IdempotencyKey
) : IRequest<SyncCartResponse>;

public sealed record SyncCartItemInput(
    Guid ShopId,
    Guid ProductId,
    Guid? VariantId,
    Guid? SessionId,
    int Quantity,
    long UnitPriceMinor  // snapshot for PRICE_CHANGED warning detection only
);

public sealed record SyncCartResponse(
    Refahi.Modules.Store.Application.Contracts.Dtos.Cart.CartDto Cart,
    IReadOnlyList<CartSyncWarning> Warnings
);

public sealed record CartSyncWarning(
    string Code,        // PRODUCT_DELETED | OUT_OF_STOCK | QUANTITY_CLAMPED |
                        // PRICE_CHANGED | SHOP_MISMATCH_DROPPED |
                        // VARIANT_REMOVED | SESSION_REMOVED
    string Message,     // Persian — user-facing
    Guid? ProductId,
    Guid? VariantId,
    Guid? SessionId
);
