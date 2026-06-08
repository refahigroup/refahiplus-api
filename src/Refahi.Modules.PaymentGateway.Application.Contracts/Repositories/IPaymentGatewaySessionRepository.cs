using Refahi.Modules.PaymentGateway.Domain.Aggregates;
using Refahi.Modules.PaymentGateway.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.PaymentGateway.Application.Contracts.Repositories;

public interface IPaymentGatewaySessionRepository
{
    Task<PaymentGatewaySession?> GetByIdAsync(Guid sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentGatewaySession>> GetByUserAsync(
        Guid userId,
        int take,
        PaymentSessionStatus? status = null,
        CancellationToken ct = default);
    Task AddAsync(PaymentGatewaySession session, CancellationToken ct = default);
    Task UpdateAsync(PaymentGatewaySession session, CancellationToken ct = default);
}
