using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;

namespace Refahi.Modules.Store.Application.Contracts.Queries.Sessions;

public sealed record GetProductSessionsQuery(Guid ProductId) : IRequest<List<ProductSessionDto>>;
