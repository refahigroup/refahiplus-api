using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Products;

public sealed record UpdateVariantAttributeCommand(
    Guid ProductId,
    Guid AttributeId,
    string Name,
    int SortOrder) : IRequest<Unit>;
