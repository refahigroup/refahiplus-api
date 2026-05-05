using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.ShopProducts;

public sealed record EnableShopProductCommand(Guid ShopId, Guid ProductId) : IRequest<Unit>;

public sealed record DisableShopProductCommand(Guid ShopId, Guid ProductId) : IRequest<Unit>;
