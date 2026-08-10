using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Checkout;

public sealed record PlaceStoreOrderV3Command(
    Guid UserId,
    int ModuleId,
    string IdempotencyKey,
    Guid? ShippingAddressId = null,
    DateOnly? DeliveryDate = null,
    short DeliveryTimeSlot = 0,
    Dictionary<Guid, short>? CartItemDeliveryMethods = null
) : IRequest<PlaceStoreOrderV3Response>;

public sealed record PlaceStoreOrderV3Response(
    Guid StoreOrderId,
    Guid OrderId,
    string OrderNumber,
    long FinalAmountMinor,
    string Status,
    string CheckoutDestination
);

public sealed record OfferChangedDetail(
    Guid CartItemId,
    Guid SnapshotOfferId,
    long SnapshotPriceMinor,
    Guid? CurrentOfferId,
    long? CurrentPriceMinor,
    string Reason
);
