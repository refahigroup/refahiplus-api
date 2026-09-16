using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Refahi.Modules.PaymentGateway.Domain.Aggregates;
using Refahi.Modules.PaymentGateway.Domain.Enums;

namespace Refahi.Modules.PaymentGateway.Application.Contracts.Repositories;

public interface IPaymentGatewaySessionRepository
{
    Task<PaymentGatewaySession?> GetByIdAsync(Guid sessionId, CancellationToken ct = default);
    Task<PaymentGatewaySession?> GetByInitiationKeyAsync(
        Guid userId,
        string idempotencyKey,
        CancellationToken ct = default);
    Task<IReadOnlyList<PaymentGatewaySession>> GetByUserAsync(
        Guid userId,
        int take,
        PaymentSessionStatus? status = null,
        CancellationToken ct = default
    );
    Task<bool> TryAddAsync(PaymentGatewaySession session, CancellationToken ct = default);
    Task<bool> TryClaimAsync(
        Guid sessionId,
        IReadOnlyCollection<PaymentSessionStatus> allowedStatuses,
        DateTimeOffset now,
        DateTimeOffset leaseUntil,
        CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetRecoveryCandidatesAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetReversalCandidatesAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken ct = default);
    Task<bool> TryClaimReversalAsync(Guid sessionId, CancellationToken ct = default);
    Task UpdateAsync(PaymentGatewaySession session, CancellationToken ct = default);
}
