using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Shops;

public sealed record ApproveShopCommand(Guid ShopId) : IRequest<ApproveShopResponse>;

public sealed record ApproveShopResponse(Guid Id, string Status);
