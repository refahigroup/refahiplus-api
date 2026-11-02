using Refahi.Modules.Entertainments.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace Refahi.Modules.Entertainments.Application.Features.Cart;

internal static class Helpers
{
    internal static async Task<EntertainmentCart> RequiredCart(IEntertainmentRepository repository, Guid userId, CancellationToken ct) => 
        await repository.GetCartAsync(userId, ct)
        ?? throw new EntertainmentDomainException("سبد خرید یافت نشد", "CART_NOT_FOUND");
}
