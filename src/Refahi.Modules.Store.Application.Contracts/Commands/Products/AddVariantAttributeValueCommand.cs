using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Products;

public sealed record AddVariantAttributeValueCommand(
    Guid ProductId,
    Guid AttributeId,
    string Value,
    int SortOrder
) : IRequest<AddVariantAttributeValueResponse>;

public sealed record AddVariantAttributeValueResponse(Guid ValueId, string Value);
