using MediatR;

namespace Refahi.Modules.Commerce.Application.Features.Checkout.Prepare;

public sealed record PrepareCommerceCheckoutCommand(
    Guid UserId, 
    string RecipientName, 
    string RecipientMobile,
    string IdempotencyKey
) : IRequest<PrepareCommerceCheckoutResponse>;



