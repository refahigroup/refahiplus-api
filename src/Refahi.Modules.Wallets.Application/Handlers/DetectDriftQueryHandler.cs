using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Queries;
using Refahi.Modules.Wallets.Application.Contracts.Responses;
using Refahi.Modules.Wallets.Application.Services;

namespace Refahi.Modules.Wallets.Application.Handlers;

public sealed class DetectDriftQueryHandler 
    : IRequestHandler<DetectDriftQuery, CommandResponse<DriftReportResponse>>
{
    private readonly BalanceRebuildApplicationService _service;

    public DetectDriftQueryHandler(BalanceRebuildApplicationService service)
    {
        _service = service;
    }

    public async Task<CommandResponse<DriftReportResponse>> Handle(
        DetectDriftQuery request,
        CancellationToken cancellationToken)
    {
        return await _service.DetectDriftAsync(request, cancellationToken);
    }
}
