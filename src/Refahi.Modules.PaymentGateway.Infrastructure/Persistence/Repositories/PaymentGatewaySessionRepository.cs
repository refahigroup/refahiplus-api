using Microsoft.EntityFrameworkCore;
using Refahi.Modules.PaymentGateway.Application.Contracts.Repositories;
using Refahi.Modules.PaymentGateway.Domain.Aggregates;
using Refahi.Modules.PaymentGateway.Domain.Enums;
using Refahi.Modules.PaymentGateway.Infrastructure.Persistence.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.PaymentGateway.Infrastructure.Persistence.Repositories;

public class PaymentGatewaySessionRepository : IPaymentGatewaySessionRepository
{
    private readonly PaymentGatewayDbContext _context;

    public PaymentGatewaySessionRepository(PaymentGatewayDbContext context)
    {
        _context = context;
    }

    public async Task<PaymentGatewaySession?> GetByIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await _context.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
    }

    public async Task<IReadOnlyList<PaymentGatewaySession>> GetByUserAsync(
        Guid userId,
        int take,
        PaymentSessionStatus? status = null,
        CancellationToken ct = default)
    {
        var query = _context.Sessions
            .AsNoTracking()
            .Where(s => s.UserId == userId);

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        return await query
            .OrderByDescending(s => s.InitiatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task AddAsync(PaymentGatewaySession session, CancellationToken ct = default)
    {
        await _context.Sessions.AddAsync(session, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(PaymentGatewaySession session, CancellationToken ct = default)
    {
        _context.Sessions.Update(session);
        await _context.SaveChangesAsync(ct);
    }
}
