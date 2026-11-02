using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Domain;

namespace Refahi.Modules.Entertainments.Application.Features.Cart;

internal static class Mapper
{
    internal static string SelectionVersion(IEnumerable<EntertainmentCartItem> items) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(items.OrderBy(x => x.Id).Select(x => new
        { x.Id, x.ProviderKey, x.SupplierKey, x.ProgramKey, x.OfferKey, x.PurchaseOptionKey, x.Quantity, x.ExpectedUnitPriceMinor })))));

    internal static EntertainmentCartDto Map(EntertainmentCart? cart) => 
        cart is null 
            ? new(Guid.Empty, [], 0) 
            : new(
                cart.Id,
                cart.Items.Select(x => new EntertainmentCartItemDto(
                    x.Id, 
                    x.ProviderKey, 
                    x.SupplierKey, 
                    x.ProgramKey, 
                    x.OfferKey,
                    x.PurchaseOptionKey, 
                    x.Title, 
                    x.SupplierTitle,
                    x.ProgramTitle,
                    x.ProgramImageUrl,
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
