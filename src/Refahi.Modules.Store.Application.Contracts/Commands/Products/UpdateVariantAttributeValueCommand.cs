using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Products;

public sealed record UpdateVariantAttributeValueCommand(
    Guid ProductId,
    Guid AttributeId,
    Guid ValueId,
    string Value,
    int SortOrder) : IRequest<Unit>;
