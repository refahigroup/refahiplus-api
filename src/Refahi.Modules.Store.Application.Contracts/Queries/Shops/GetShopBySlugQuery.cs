using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Shops;

namespace Refahi.Modules.Store.Application.Contracts.Queries.Shops;

public sealed record GetShopBySlugQuery(string Slug) : IRequest<ShopDto?>;
