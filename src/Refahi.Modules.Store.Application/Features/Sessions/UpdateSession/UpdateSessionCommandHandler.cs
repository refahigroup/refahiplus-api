using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Sessions;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Sessions.UpdateSession;

public class UpdateSessionCommandHandler : IRequestHandler<UpdateSessionCommand, UpdateSessionResponse>
{
    private readonly IProductSessionRepository _sessionRepo;

    public UpdateSessionCommandHandler(IProductSessionRepository sessionRepo)
        => _sessionRepo = sessionRepo;

    public async Task<UpdateSessionResponse> Handle(UpdateSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepo.GetByIdAsync(request.SessionId, cancellationToken)
            ?? throw new StoreDomainException("سانس یافت نشد", "SESSION_NOT_FOUND");

        session.UpdateInfo(request.Capacity, request.Title, request.PriceAdjustment);

        if (request.IsActive)
            session.Activate();
        else
            session.Deactivate();

        await _sessionRepo.UpdateAsync(session, cancellationToken);

        return new UpdateSessionResponse(session.Id);
    }
}
