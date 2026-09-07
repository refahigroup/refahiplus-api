using MediatR;

namespace Refahi.Modules.Commerce.Application.Features.Checkout.GetContact;

public sealed record GetCommerceCheckoutContactQuery(
    Guid UserId
) : IRequest<CommerceCheckoutContactDto>;