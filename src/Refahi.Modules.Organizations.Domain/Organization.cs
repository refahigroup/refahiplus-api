namespace Refahi.Modules.Organizations.Domain;

public sealed class Organization
{
    private Organization() { }
    public Guid Id { get; private set; }
    public string Code { get; private set; } = "";
    public string Title { get; private set; } = "";
    public bool IsActive { get; private set; }
    public static Organization Create(string code, string title)
    {
        var value = new Organization { Id = Guid.NewGuid() }; value.Update(code, title, true); return value;
    }
    public void Update(string code, string title, bool active)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 50 || string.IsNullOrWhiteSpace(title) || title.Length > 200)
            throw new ArgumentException("عنوان و کد سازمان معتبر نیست");
        Code = code.Trim().ToUpperInvariant(); Title = title.Trim(); IsActive = active;
    }
}

public sealed class OrganizationMembership
{
    private OrganizationMembership() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? EndedBy { get; private set; }
    public DateTimeOffset ValidFrom { get; private set; }
    public DateTimeOffset? ValidUntil { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public static OrganizationMembership Create(Guid organizationId, Guid userId, Guid actor, DateTimeOffset from, DateTimeOffset? until)
    {
        if (userId == Guid.Empty || until <= from) throw new ArgumentException("بازهٔ عضویت معتبر نیست");
        return new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            CreatedBy = actor,
            ValidFrom = from.ToUniversalTime(),
            ValidUntil = until?.ToUniversalTime()
        };
    }
    public void Revoke(Guid actor, DateTimeOffset now) { RevokedAt ??= now; EndedBy ??= actor; }
}

public sealed class OrganizationAudit
{
    private OrganizationAudit() { }
    public Guid Id { get; private set; }
    public Guid ActorId { get; private set; }
    public Guid SubjectId { get; private set; }
    public string Action { get; private set; } = "";
    public string Details { get; private set; } = "";
    public DateTimeOffset OccurredAt { get; private set; }
    public static OrganizationAudit Create(Guid actor, Guid subject, string action, string details, DateTimeOffset now) =>
        new() { Id = Guid.NewGuid(), ActorId = actor, SubjectId = subject, Action = action, Details = details, OccurredAt = now };
}
