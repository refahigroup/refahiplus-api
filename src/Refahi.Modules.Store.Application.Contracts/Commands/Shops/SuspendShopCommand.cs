using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Shops;

public sealed record SuspendShopCommand(Guid ShopId) : IRequest<SuspendShopResponse>;

public sealed record SuspendShopResponse(Guid Id, string Status);
