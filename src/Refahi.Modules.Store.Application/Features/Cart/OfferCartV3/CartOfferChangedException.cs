using Refahi.Modules.Store.Application.Contracts.Commands.Cart;
using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Application.Features.Cart.OfferCartV3;

public sealed class CartOfferChangedException(IReadOnlyList<CartOfferChangedDetail> details)
    : StoreDomainException("پیشنهاد یا قیمت سبد تغییر کرده است؛ تغییرات را بررسی و تأیید کنید", "OFFER_CHANGED")
{
    public IReadOnlyList<CartOfferChangedDetail> Details { get; } = details;
}
