using MediatR;

namespace Refahi.Modules.Commerce.Application.Features.Operations.GetOperation;

public sealed record GetCommerceOperationsQuery(
    string? Status,
    int PageNumber = 1,
    int PageSize = 50
) : IRequest<IReadOnlyList<CommerceOperationDto>>;