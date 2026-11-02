namespace Refahi.Modules.Hotels.Domain.Aggregates.Catalogue;

public enum HotelExternalOperationKind { Submit, Confirm, Inquiry }
public enum HotelExternalOperationState { Started, Succeeded, Rejected, Unknown }

public sealed class HotelExternalOperation
{
    private HotelExternalOperation() { }
    public Guid Id { get; private set; }
    public Guid HotelRequestId { get; private set; }
    public string ReferenceId { get; private set; } = "";
    public HotelExternalOperationKind Kind { get; private set; }
    public HotelExternalOperationState State { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? ResponseSnapshot { get; private set; }
    public string? ErrorCode { get; private set; }
    public int Version { get; private set; }
    public static HotelExternalOperation Start(HotelExternalReservation reservation, HotelExternalOperationKind kind) =>
        new() { Id = Guid.NewGuid(), HotelRequestId = reservation.HotelRequestId, ReferenceId = reservation.ReferenceId,
            Kind = kind, State = HotelExternalOperationState.Started, StartedAtUtc = DateTime.UtcNow };
    public void Finish(HotelExternalOperationState state, string? snapshot = null, string? errorCode = null)
    {
        if (State != HotelExternalOperationState.Started || state == HotelExternalOperationState.Started) return;
        State = state; ResponseSnapshot = snapshot; ErrorCode = errorCode; CompletedAtUtc = DateTime.UtcNow; Version++;
    }
}
