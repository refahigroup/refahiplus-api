using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;

namespace Refahi.Modules.Store.Application.Contracts.Queries.Products;

public sealed record GetProductBySlugQuery(
    string Slug,
    string? ShopSlug = null,
    Guid? ShopId = null) : IRequest<ProductDetailDto?>;
