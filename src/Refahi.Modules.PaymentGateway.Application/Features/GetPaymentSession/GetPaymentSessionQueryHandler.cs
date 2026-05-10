using MediatR;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.GetPaymentSession;
using Refahi.Modules.PaymentGateway.Application.Contracts.Repositories;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.PaymentGateway.Application.Features.GetPaymentSession;

public class GetPaymentSessionQueryHandler : IRequestHandler<GetPaymentSessionQuery, PaymentSessionDto?>
{
    private readonly IPaymentGatewaySessionRepository _sessionRepository;

    public GetPaymentSessionQueryHandler(IPaymentGatewaySessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository;
    }

    public async Task<PaymentSessionDto?> Handle(GetPaymentSessionQuery query, CancellationToken ct)
    {
        var session = await _sessionRepository.GetByIdAsync(query.SessionId, ct);

        if (session is null)
            return null;

        // Ownership check — only the session owner can read their session
        if (session.UserId != query.RequestingUserId)
            return null;

        return new PaymentSessionDto(
            SessionId: session.Id,
            Status: session.Status,
            AmountMinor: session.AmountMinor,
            Currency: session.Currency,
            Provider: session.Provider,
            InitiatedAt: session.InitiatedAt,
            CompletedAt: session.CompletedAt,
            TopUpLedgerEntryId: session.TopUpLedgerEntryId,
            ProviderResultCode: session.ProviderResultCode,
            ProviderResultDescription: session.ProviderResultDescription);
    }
}
