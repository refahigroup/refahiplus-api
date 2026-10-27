using MediatR;

namespace Refahi.Modules.Entertainments.Application.Features.Cart.Clear;

public sealed record ClearEntertainmentCartCommand(
    Guid UserId
) : IRequest;

