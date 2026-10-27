using MediatR;

namespace Refahi.Modules.Entertainments.Application.Features.Operations.GetOperation;

public sealed record GetEntertainmentOperationsQuery(
    string? Status,
    int PageNumber = 1,
    int PageSize = 50
) : IRequest<IReadOnlyList<EntertainmentOperationDto>>;