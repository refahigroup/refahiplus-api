namespace Refahi.Modules.Pools.Domain;
public enum PoolTicketOperationStatus { Sending, Reserved, Rejected, Unknown }
public sealed class PoolTicketReservation
{
    private PoolTicketReservation() { }
    public Guid RequestId { get; private set; }
    public PoolTicketOperationStatus Status { get; private set; }
    public string Mobile { get; private set; } = "";
    public string? ReservationId { get; private set; }
    public string? VoucherCiphertext { get; private set; }
    public int? Enter { get; private set; }
    public string? ErrorCode { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public static PoolTicketReservation Begin(Guid request, string mobile, DateTimeOffset now) =>
        new() { RequestId = request, Mobile = mobile, StartedAt = now, Status = PoolTicketOperationStatus.Sending };
    public void Reserve(string id, string voucherCiphertext, int enter, DateTimeOffset now)
    { ReservationId = id; VoucherCiphertext = voucherCiphertext; Enter = enter; CompletedAt = now; Status = PoolTicketOperationStatus.Reserved; }
    public void Fail(bool definite, string code, DateTimeOffset now)
    { Status = definite ? PoolTicketOperationStatus.Rejected : PoolTicketOperationStatus.Unknown; ErrorCode = code; CompletedAt = now; }
}
