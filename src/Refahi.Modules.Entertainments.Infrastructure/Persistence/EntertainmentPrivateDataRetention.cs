using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Domain;
namespace Refahi.Modules.Entertainments.Infrastructure.Persistence;

public sealed class EntertainmentPrivateDataRetention(EntertainmentDbContext db) : IEntertainmentPrivateDataRetention
{
    public async Task PruneAsync(DateTimeOffset cutoff, CancellationToken ct)
    {
        var purchases = await db.Orders.AsNoTracking().Where(x => x.CreatedAt < cutoff &&
            (x.Status == EntertainmentPurchaseStatus.Completed || x.Status == EntertainmentPurchaseStatus.Cancelled || x.Status == EntertainmentPurchaseStatus.Refunded))
            .Select(x => new { x.Id, x.CheckoutSessionId }).ToListAsync(ct);
        var ids = purchases.Select(x => x.Id).ToArray();
        var fulfillments = await db.Fulfillments.Where(x => ids.Contains(x.EntertainmentPurchaseId)).Select(x => x.Id).ToListAsync(ct);
        var released = await db.CheckoutSessions.Where(x => x.CreatedAt < cutoff &&
            (x.Status == EntertainmentSessionStatus.Released || x.Status == EntertainmentSessionStatus.Expired)).Select(x => x.Id).ToListAsync(ct);
        var keys = fulfillments.Select(x => $"entertainments-fulfill:{x:N}")
            .Concat(purchases.Where(x => x.CheckoutSessionId.HasValue).Select(x => $"reserve:{x.CheckoutSessionId!.Value:N}"))
            .Concat(released.Select(x => $"reserve:{x:N}")).ToArray();
        foreach (var receipt in await db.ProviderReceipts.Where(x => keys.Contains(x.OperationId) && x.PayloadProtected != "").Take(100).ToListAsync(ct)) receipt.Redact();
        foreach (var attempt in await db.OperationAttempts.Where(x => fulfillments.Contains(x.ProviderFulfillmentId) && x.EvidenceProtected != null).Take(100).ToListAsync(ct)) attempt.RedactEvidence();
        foreach (var session in await db.CheckoutSessions.Where(x => x.ResolutionEvidenceProtected != null && x.ResolvedAtUtc < cutoff).Take(100).ToListAsync(ct)) session.RedactResolutionEvidence();
        await db.SaveChangesAsync(ct);
    }
}
