using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Shops;

namespace Refahi.Modules.Store.Application.Contracts.Queries.Shops;

public sealed record AdminGetShopQuery(Guid ShopId) : IRequest<ShopDto?>;
