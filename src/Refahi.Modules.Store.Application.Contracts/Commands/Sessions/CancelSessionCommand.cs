using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Sessions;

public sealed record CancelSessionCommand(Guid SessionId) : IRequest<CancelSessionResponse>;

public sealed record CancelSessionResponse(Guid SessionId);
