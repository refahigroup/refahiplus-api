using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Products;

public sealed record DeleteVariantAttributeValueCommand(
    Guid ProductId,
    Guid AttributeId,
    Guid ValueId) : IRequest<Unit>;