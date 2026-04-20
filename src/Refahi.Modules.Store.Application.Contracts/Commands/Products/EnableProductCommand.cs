using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Products;

public sealed record EnableProductCommand(Guid Id) : IRequest<EnableProductResponse>;

public sealed record EnableProductResponse(Guid Id, bool IsDeleted);
