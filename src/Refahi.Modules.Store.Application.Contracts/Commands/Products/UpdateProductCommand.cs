using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Products;

public sealed record UpdateProductCommand(
    Guid Id,
    string Title,
    string? Description,
    bool IsAvailable
) : IRequest<UpdateProductResponse>;

public sealed record UpdateProductResponse(Guid Id, string Title);
