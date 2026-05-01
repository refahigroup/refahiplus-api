using MediatR;

namespace Refahi.Modules.Media.Application.Contracts.Commands;

public sealed record DeleteMediaCommand(
    Guid MediaId,
    Guid RequestedByUserId,
    bool IsAdmin
) : IRequest;
