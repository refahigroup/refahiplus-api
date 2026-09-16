using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Refahi.Modules.PaymentGateway.Application.Contracts.Exceptions;
using Refahi.Modules.PaymentGateway.Application.Contracts.Repositories;
using Refahi.Modules.PaymentGateway.Domain.Aggregates;
using Refahi.Modules.PaymentGateway.Domain.Enums;
using Refahi.Modules.PaymentGateway.Infrastructure.Persistence.Context;

namespace Refahi.Modules.PaymentGateway.Infrastructure.Persistence.Repositories;

public class PaymentGatewaySessionRepository : IPaymentGatewaySessionRepository
{
    private readonly PaymentGatewayDbContext _context;

    public PaymentGatewaySessionRepository(PaymentGatewayDbContext context)
    {
        _context = context;
    }

    public async Task<PaymentGatewaySession?> GetByIdAsync(
        Guid sessionId,
        CancellationToken ct = default
    )
    {
        return await _context.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
    }

    public Task<PaymentGatewaySession?> GetByInitiationKeyAsync(
        Guid userId,
        string idempotencyKey,
        CancellationToken ct = default) =>
        _context.Sessions.FirstOrDefaultAsync(
            s => s.UserId == userId && s.InitiationIdempotencyKey == idempotencyKey,
            ct);

    public async Task<IReadOnlyList<PaymentGatewaySession>> GetByUserAsync(
        Guid userId,
        int take,
        PaymentSessionStatus? status = null,
        CancellationToken ct = default
    )
    {
        var query = _context.Sessions.AsNoTracking().Where(s => s.UserId == userId);

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        return await query.OrderByDescending(s => s.InitiatedAt).Take(take).ToListAsync(ct);
    }

    public async Task<bool> TryAddAsync(PaymentGatewaySession session, CancellationToken ct = default)
    {
        try
        {
            await _context.Sessions.AddAsync(session, ct);
            await _context.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, "uq_pg_sessions_user_idempotency"))
        {
            _context.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task<bool> TryClaimAsync(
        Guid sessionId,
        IReadOnlyCollection<PaymentSessionStatus> allowedStatuses,
        DateTimeOffset now,
        DateTimeOffset leaseUntil,
        CancellationToken ct = default)
    {
        var statuses = allowedStatuses.ToArray();
        var affected = await _context.Sessions
            .Where(s => s.Id == sessionId
                && statuses.Contains(s.Status)
                && (s.Status != PaymentSessionStatus.Processing
                    || s.ProcessingLeaseUntil == null
                    || s.ProcessingLeaseUntil <= now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Status, PaymentSessionStatus.Processing)
                .SetProperty(s => s.ProcessingLeaseUntil, leaseUntil)
                .SetProperty(s => s.Version, s => s.Version + 1), ct);
        _context.ChangeTracker.Clear();
        return affected == 1;
    }

    public async Task<IReadOnlyList<Guid>> GetRecoveryCandidatesAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken ct = default) =>
        await _context.Sessions.AsNoTracking()
            .Where(s =>
                (s.Status == PaymentSessionStatus.PendingRecovery
                    && (s.NextRecoveryAt == null || s.NextRecoveryAt <= now))
                || (s.Status == PaymentSessionStatus.Processing
                    && s.ProcessingLeaseUntil != null && s.ProcessingLeaseUntil <= now)
                || (s.Status == PaymentSessionStatus.TokenReceived && s.ExpiresAt <= now))
            .OrderBy(s => s.NextRecoveryAt ?? s.ProcessingLeaseUntil)
            .Select(s => s.Id)
            .Take(batchSize)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetReversalCandidatesAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken ct = default) =>
        await _context.Sessions.AsNoTracking()
            .Where(s => (s.ReversalStatus == FundingReversalStatus.Required
                    || s.ReversalStatus == FundingReversalStatus.Failed)
                && (s.NextReversalAt == null || s.NextReversalAt <= now))
            .OrderBy(s => s.NextReversalAt)
            .Select(s => s.Id)
            .Take(batchSize)
            .ToListAsync(ct);

    public async Task<bool> TryClaimReversalAsync(
        Guid sessionId,
        CancellationToken ct = default)
    {
        var affected = await _context.Sessions
            .Where(s => s.Id == sessionId &&
                (s.ReversalStatus == FundingReversalStatus.Required
                    || s.ReversalStatus == FundingReversalStatus.Failed))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.ReversalStatus, FundingReversalStatus.Pending)
                .SetProperty(s => s.Version, s => s.Version + 1), ct);
        _context.ChangeTracker.Clear();
        return affected == 1;
    }

    public async Task UpdateAsync(PaymentGatewaySession session, CancellationToken ct = default)
    {
        session.AdvanceVersion();
        _context.Sessions.Update(session);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, "uq_pg_sessions_provider_transaction"))
        {
            _context.ChangeTracker.Clear();
            throw new ProviderTransactionAlreadyClaimedException();
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex, string constraintName) =>
        ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: var actual
        } && string.Equals(actual, constraintName, StringComparison.Ordinal);
}
