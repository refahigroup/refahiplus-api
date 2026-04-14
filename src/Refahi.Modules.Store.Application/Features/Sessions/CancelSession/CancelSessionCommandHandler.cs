using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Sessions;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Sessions.CancelSession;

public class CancelSessionCommandHandler : IRequestHandler<CancelSessionCommand, CancelSessionResponse>
{
    private readonly IProductSessionRepository _sessionRepo;

    public CancelSessionCommandHandler(IProductSessionRepository sessionRepo)
        => _sessionRepo = sessionRepo;

    public async Task<CancelSessionResponse> Handle(CancelSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepo.GetByIdAsync(request.SessionId, cancellationToken)
            ?? throw new StoreDomainException("سانس یافت نشد", "SESSION_NOT_FOUND");

        session.Cancel();

        await _sessionRepo.UpdateAsync(session, cancellationToken);

        return new CancelSessionResponse(session.Id);
    }
}
