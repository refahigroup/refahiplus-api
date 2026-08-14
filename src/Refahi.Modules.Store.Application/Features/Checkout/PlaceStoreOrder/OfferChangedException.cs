using Refahi.Modules.Store.Application.Contracts.Commands.Checkout;
using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Application.Features.Checkout.PlaceStoreOrder;

public sealed class OfferChangedException(IReadOnlyList<OfferChangedDetail> details)
    : StoreDomainException(
        "پیشنهاد یا قیمت برخی اقلام تغییر کرده است؛ سبد خرید را بررسی کنید",
        "OFFER_CHANGED"
    )
{
    public IReadOnlyList<OfferChangedDetail> Details { get; } = details;
}
