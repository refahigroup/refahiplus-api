using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.ShopProducts;

public sealed record RemoveProductFromShopCommand(Guid ShopId, Guid ProductId) : IRequest;
