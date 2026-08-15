using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Domain.Entities;

public sealed class VoucherCodeImportBatch
{
    private VoucherCodeImportBatch() { }

    public Guid Id { get; private set; }
    public Guid VoucherSourceId { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RequestFingerprint { get; private set; } = string.Empty;
    public int TotalCount { get; private set; }
    public int AcceptedCount { get; private set; }
    public int DuplicateCount { get; private set; }
    public int RejectedCount { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static VoucherCodeImportBatch Create(
        Guid sourceId,
        Guid supplierId,
        Guid actorUserId,
        string idempotencyKey,
        string requestFingerprint,
        int total,
        int accepted,
        int duplicate,
        int rejected,
        DateTimeOffset nowUtc)
    {
        if (sourceId == Guid.Empty || supplierId == Guid.Empty || actorUserId == Guid.Empty
            || string.IsNullOrWhiteSpace(idempotencyKey)
            || string.IsNullOrWhiteSpace(requestFingerprint)
            || total < 0 || accepted < 0 || duplicate < 0 || rejected < 0
            || total != accepted + duplicate + rejected)
            throw new StoreDomainException("اطلاعات دسته ورود کد معتبر نیست", "INVALID_VOUCHER_IMPORT_BATCH");
        return new VoucherCodeImportBatch
        {
            Id = Guid.NewGuid(),
            VoucherSourceId = sourceId,
            SupplierId = supplierId,
            ActorUserId = actorUserId,
            IdempotencyKey = idempotencyKey.Trim(),
            RequestFingerprint = requestFingerprint,
            TotalCount = total,
            AcceptedCount = accepted,
            DuplicateCount = duplicate,
            RejectedCount = rejected,
            CreatedAtUtc = nowUtc,
        };
    }
}
