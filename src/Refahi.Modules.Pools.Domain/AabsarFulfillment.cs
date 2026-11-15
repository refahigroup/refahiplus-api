namespace Refahi.Modules.Pools.Domain;

public enum AabsarOperation { Ready, Sending, Issued, Rejected, IssueUnknown, Cancelling, Cancelled, CancelUnknown }

public sealed class AabsarFulfillment
{
    private AabsarFulfillment() { }
    public Guid PurchaseId { get; private set; }
    public string? ContactCiphertext { get; private set; }
    public string CartSnapshot { get; private set; } = "";
    public string? ReceiptCiphertext { get; private set; }
    public AabsarOperation Status { get; private set; }
    public string? IssueFingerprint { get; private set; }
    public string? CancelFingerprint { get; private set; }
    public string? EvidenceFingerprint { get; private set; }

    public static AabsarFulfillment Create(Guid id, string contact, string cartSnapshot)
        => new() { PurchaseId = id, ContactCiphertext = contact, CartSnapshot = cartSnapshot };

    public void BeginIssue(string fingerprint)
    {
        if (Status != AabsarOperation.Ready) throw new InvalidOperationException();
        IssueFingerprint = fingerprint;
        Status = AabsarOperation.Sending;
    }

    public void BeginCancel(string fingerprint)
    {
        if (Status != AabsarOperation.Issued) throw new InvalidOperationException();
        CancelFingerprint = fingerprint;
        Status = AabsarOperation.Cancelling;
    }

    public void Resolve(AabsarOperation status, string? receipt = null, string? evidence = null)
    {
        Status = status;
        if (receipt is not null) ReceiptCiphertext = receipt;
        if (evidence is not null) EvidenceFingerprint = evidence;
    }

    public void RedactContact() => ContactCiphertext = null;
}
