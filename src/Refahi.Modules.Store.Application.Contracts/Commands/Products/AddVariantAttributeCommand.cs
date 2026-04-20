using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Products;

public sealed record AddVariantAttributeCommand(
    Guid ProductId,
    string Name,
    int SortOrder
) : IRequest<AddVariantAttributeResponse>;

public sealed record AddVariantAttributeResponse(Guid AttributeId, string Name);
