using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;

namespace Refahi.Modules.Store.Application.Contracts.Queries.Products;

public sealed record AdminGetProductQuery(Guid ProductId) : IRequest<ProductDetailDto?>;
