using MediatR;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.GetMyPaymentSessions;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.GetPaymentSession;
using Refahi.Modules.PaymentGateway.Application.Contracts.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.PaymentGateway.Application.Features.GetMyPaymentSessions;

public sealed class GetMyPaymentSessionsQueryHandler
    : IRequestHandler<GetMyPaymentSessionsQuery, IReadOnlyList<PaymentSessionDto>>
{
    private readonly IPaymentGatewaySessionRepository _sessionRepository;

    public GetMyPaymentSessionsQueryHandler(IPaymentGatewaySessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository;
    }

    public async Task<IReadOnlyList<PaymentSessionDto>> Handle(
        GetMyPaymentSessionsQuery request,
        CancellationToken ct)
    {
        var take = request.Take is < 1 or > 100 ? 20 : request.Take;
        var sessions = await _sessionRepository.GetByUserAsync(request.UserId, take, request.Status, ct);

        return sessions.Select(session => new PaymentSessionDto(
            SessionId: session.Id,
            Status: session.Status,
            AmountMinor: session.AmountMinor,
            Currency: session.Currency,
            Provider: session.Provider,
            InitiatedAt: session.InitiatedAt,
            CompletedAt: session.CompletedAt,
            TopUpLedgerEntryId: session.TopUpLedgerEntryId,
            ProviderResultCode: session.ProviderResultCode,
            ProviderResultDescription: session.ProviderResultDescription)).ToList();
    }
}
