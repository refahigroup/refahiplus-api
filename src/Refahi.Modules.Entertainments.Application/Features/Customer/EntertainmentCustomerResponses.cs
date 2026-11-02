using Refahi.Modules.Entertainments.Application.Contracts.Discovery;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;
using Refahi.Modules.Entertainments.Application.Features.Cart;
using Refahi.Modules.Entertainments.Application.Features.Checkout.Sessions;
namespace Refahi.Modules.Entertainments.Application.Features.Customer;

public sealed record CustomerOffer(string ProgramKey, string OfferKey, string Title, string? Subtitle, DateTimeOffset? StartsAt, int? Capacity, IReadOnlyList<EntertainmentPurchaseOptionDto> PurchaseOptions);
public sealed record CustomerCartItem(Guid Id, string ProgramKey, string OfferKey, string PurchaseOptionKey, string Title, string SupplierTitle, string ProgramTitle, string? ProgramImageUrl, string OptionTitle, string OfferTitle, int Quantity, long UnitPriceMinor, long OriginalUnitPriceMinor, bool IsAvailable);
public sealed record CustomerCheckoutGroup(IReadOnlyList<Guid> CartItemIds, string Version);
public sealed record CustomerCart(Guid CartId, IReadOnlyList<CustomerCartItem> Items, long TotalAmountMinor, IReadOnlyList<CustomerCheckoutGroup> CheckoutGroups);
public sealed record CustomerSession(Guid Id, string Status, string Version, DateTimeOffset? PayableUntil, long TotalAmountMinor, IReadOnlyList<EntertainmentSessionItemDto> Items, Guid? EntertainmentPurchaseId);
public static class EntertainmentCustomerResponses
{
    private static string Clean(string value) => EntertainmentPresentation.Text(value) ?? "";
    public static CustomerOffer Public(this EntertainmentOfferDto x) => new(x.ProgramKey, x.OfferKey, Clean(x.Title), EntertainmentPresentation.Text(x.Subtitle), x.StartsAt, x.Capacity,
        x.PurchaseOptions.Select(o => o with { Title = Clean(o.Title) }).ToArray());
    public static CustomerCart Public(this EntertainmentCartDto x) => new(x.CartId,
        x.Items.Select(i => new CustomerCartItem(i.Id, i.ProgramKey, i.OfferKey, i.PurchaseOptionKey, Clean(i.Title), Clean(i.SupplierTitle), Clean(i.ProgramTitle), i.ProgramImageUrl, Clean(i.OptionTitle), Clean(i.OfferTitle), i.Quantity, i.UnitPriceMinor, i.OriginalUnitPriceMinor, i.IsAvailable)).ToArray(), x.TotalAmountMinor,
        x.Items.GroupBy(i => i.ProviderKey, StringComparer.OrdinalIgnoreCase).Select(g => new CustomerCheckoutGroup(g.Select(i => i.Id).Order().ToArray(), x.CheckoutVersions[g.Key])).ToArray());
    public static CustomerSession Public(this EntertainmentSessionDto x) => new(x.Id, x.Status, x.Version, x.PayableUntil, x.TotalAmountMinor,
        x.Items.Select(i => i with { ProgramTitle = Clean(i.ProgramTitle), OptionTitle = Clean(i.OptionTitle) }).ToArray(), x.EntertainmentPurchaseId);
}
