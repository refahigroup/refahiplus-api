using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Products;

public sealed record AddProductSpecificationCommand(
    Guid ProductId, string Key, string Value, int SortOrder
) : IRequest<AddProductSpecificationResponse>;

public sealed record AddProductSpecificationResponse(int SpecificationId);
