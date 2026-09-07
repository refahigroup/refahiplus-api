using Refahi.Modules.Commerce.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace Refahi.Modules.Commerce.Application.Features.Cart;

internal static class Helpers
{
    internal static async Task<CommerceCart> RequiredCart(ICommerceRepository repository, Guid userId, CancellationToken ct) => 
        await repository.GetCartAsync(userId, ct)
        ?? throw new CommerceDomainException("سبد خرید یافت نشد", "CART_NOT_FOUND");
}
