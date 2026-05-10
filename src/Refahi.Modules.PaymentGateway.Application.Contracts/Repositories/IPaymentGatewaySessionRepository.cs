using Refahi.Modules.PaymentGateway.Domain.Aggregates;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.PaymentGateway.Application.Contracts.Repositories;

public interface IPaymentGatewaySessionRepository
{
    Task<PaymentGatewaySession?> GetByIdAsync(Guid sessionId, CancellationToken ct = default);
    Task AddAsync(PaymentGatewaySession session, CancellationToken ct = default);
    Task UpdateAsync(PaymentGatewaySession session, CancellationToken ct = default);
}
