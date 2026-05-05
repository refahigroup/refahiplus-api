using MediatR;

namespace Refahi.Modules.Media.Application.Contracts.Commands;

public sealed record LinkMediaToEntityCommand(
    Guid MediaId,
    string EntityType,
    Guid EntityId,
    Guid RequestedByUserId,
    bool IsAdmin
) : IRequest<Unit>;
