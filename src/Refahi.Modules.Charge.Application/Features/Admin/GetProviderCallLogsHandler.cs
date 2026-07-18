using MediatR;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Domain.Repositories;

namespace Refahi.Modules.Charge.Application.Features.Admin;

public sealed class GetProviderCallLogsHandler : IRequestHandler<GetProviderCallLogsQuery, IReadOnlyList<ProviderCallLogDto>>
{
    private readonly IChargeRequestRepository _requests;
    private readonly IProviderCallLogRepository _logs;

    public GetProviderCallLogsHandler(IChargeRequestRepository requests, IProviderCallLogRepository logs)
    {
        _requests = requests;
        _logs = logs;
    }

    public async Task<IReadOnlyList<ProviderCallLogDto>> Handle(GetProviderCallLogsQuery query, CancellationToken ct)
    {
        _ = await _requests.GetAsync(query.RequestId, ct) ?? throw new ArgumentException("درخواست شارژ یافت نشد");
        var page = Math.Max(1, query.PageNumber);
        var size = Math.Clamp(query.PageSize, 1, 100);
        var rows = await _logs.GetForChargeRequestAsync(query.RequestId, (page - 1) * size, size, ct);
        return rows.Select(x => new ProviderCallLogDto(
            x.Id, x.ProviderName, x.Operation, x.Stage, x.Outcome.ToString(), x.HttpStatusCode,
            x.ProviderResultCode, x.OperatorResultCode, x.Retryable, x.AttemptNumber,
            x.CorrelationId, x.ErrorMessage, x.LatencyMilliseconds, x.CreatedAt)).ToList();
    }
}
