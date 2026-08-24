using MediatR;

namespace Refahi.Modules.Commerce.Application.Features.Cart.Clear;

public sealed record ClearCommerceCartCommand(
    Guid UserId
) : IRequest;

