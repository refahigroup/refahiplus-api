using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Products;

public sealed record DeleteVariantAttributeCommand(Guid ProductId, Guid AttributeId) : IRequest<Unit>;