using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Products;

public sealed record DisableProductCommand(Guid Id) : IRequest<DisableProductResponse>;

public sealed record DisableProductResponse(Guid Id, bool IsDeleted);
