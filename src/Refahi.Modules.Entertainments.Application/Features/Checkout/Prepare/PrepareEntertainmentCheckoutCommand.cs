using MediatR;

namespace Refahi.Modules.Entertainments.Application.Features.Checkout.Prepare;

public sealed record PrepareEntertainmentCheckoutCommand(
    Guid UserId,
    string IdempotencyKey,
    string? ProviderKey = null
) : IRequest<PrepareEntertainmentCheckoutResponse>;



