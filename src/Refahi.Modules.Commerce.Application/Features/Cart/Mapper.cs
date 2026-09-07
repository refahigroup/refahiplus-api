using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Domain;

namespace Refahi.Modules.Commerce.Application.Features.Cart;

internal static class Mapper
{
    internal static string SelectionVersion(IEnumerable<CommerceCartItem> items) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(items.OrderBy(x => x.Id).Select(x => new
        { x.Id, x.ProviderKey, x.SellerKey, x.ProductKey, x.OfferKey, x.PurchaseOptionKey, x.Quantity, x.ExpectedUnitPriceMinor })))));

    internal static CommerceCartDto Map(CommerceCart? cart) => 
        cart is null 
            ? new(Guid.Empty, [], 0) 
            : new(
                cart.Id,
                cart.Items.Select(x => new CommerceCartItemDto(
                    x.Id, 
                    x.ProviderKey, 
                    x.SellerKey, 
                    x.ProductKey, 
                    x.OfferKey,
                    x.PurchaseOptionKey, 
                    x.Title, 
                    x.SellerTitle,
                    x.ProductTitle,
                    x.ProductImageUrl,
                    x.OptionTitle,
                    x.OfferTitle, 
                    x.Quantity, 
                    x.ExpectedUnitPriceMinor,
                    x.OriginalUnitPriceMinor,
                    x.IsAvailable)
                ).ToArray(),
                cart.Items.Sum(x => checked(x.ExpectedUnitPriceMinor * x.Quantity))
            ) { CheckoutVersions = cart.Items.GroupBy(x => x.ProviderKey).ToDictionary(x => x.Key, x => SelectionVersion(x)) };
}
