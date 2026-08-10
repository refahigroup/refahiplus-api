using System.Security.Cryptography;
using System.Text;
using Refahi.Modules.Store.Application.Contracts.Commands.Checkout;

namespace Refahi.Modules.Store.Application.Features.Checkout.PlaceStoreOrderV3;

public static class CheckoutRequestFingerprint
{
    public static string Create(PlaceStoreOrderV3Command request)
    {
        var selections = request.CartItemDeliveryMethods is null
            ? string.Empty
            : string.Join(
                ',',
                request
                    .CartItemDeliveryMethods.OrderBy(x => x.Key)
                    .Select(x => $"{x.Key:N}:{x.Value}")
            );
        var canonical = string.Join(
            '|',
            "store-checkout-v3",
            request.ModuleId,
            request.ShippingAddressId?.ToString("N") ?? "-",
            request.DeliveryDate?.ToString("yyyy-MM-dd") ?? "-",
            request.DeliveryTimeSlot,
            selections
        );
        return Convert
            .ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }
}
