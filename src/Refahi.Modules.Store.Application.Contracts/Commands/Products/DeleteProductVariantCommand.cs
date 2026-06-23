using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Products;

public sealed record DeleteProductVariantCommand(Guid ProductId, Guid VariantId) : IRequest<Unit>;