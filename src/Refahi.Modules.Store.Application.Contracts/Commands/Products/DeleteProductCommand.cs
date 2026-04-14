using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Products;

public sealed record DeleteProductCommand(Guid Id) : IRequest<DeleteProductResponse>;

public sealed record DeleteProductResponse(Guid Id);
