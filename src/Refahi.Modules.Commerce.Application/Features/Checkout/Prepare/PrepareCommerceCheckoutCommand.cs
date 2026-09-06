using MediatR;

namespace Refahi.Modules.Commerce.Application.Features.Checkout.Prepare;

public sealed record PrepareCommerceCheckoutCommand(
    Guid UserId,
    string IdempotencyKey,
    string? ProviderKey = null
) : IRequest<PrepareCommerceCheckoutResponse>;



