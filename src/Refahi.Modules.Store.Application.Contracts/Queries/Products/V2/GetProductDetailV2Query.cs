using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products.V2;

namespace Refahi.Modules.Store.Application.Contracts.Queries.Products.V2;

public sealed record GetProductDetailV2Query(
    int ModuleId,
    string Slug,
    Guid? ShopId = null,
    string? ShopSlug = null,
    string? OfferKey = null,
    Guid? VariantId = null)
    : IRequest<ProductDetailV2Dto?>;
