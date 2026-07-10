using MediatR;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Domain.Repositories;

namespace Refahi.Modules.Charge.Application.Features;

public sealed class CancelChargeRequestHandler : IRequestHandler<CancelChargeRequestCommand, bool>
{
    private readonly IChargeRequestRepository _requests;

    public CancelChargeRequestHandler(IChargeRequestRepository requests)
    {
        _requests = requests;
    }

    public async Task<bool> Handle(CancelChargeRequestCommand command, CancellationToken ct)
    {
        var request = await _requests.GetForUserAsync(command.RequestId, command.UserId, ct);

        if (request is null) 
            return false;

        request.Cancel(DateTime.UtcNow);
        await _requests.SaveChangesAsync(ct);

        return true;
    }
}
