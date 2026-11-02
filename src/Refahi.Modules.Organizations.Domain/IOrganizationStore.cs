namespace Refahi.Modules.Organizations.Domain;

public interface IOrganizationStore
{
    Task<IOrganizationTransaction> LockAsync(CancellationToken ct);
    Task<List<Organization>> ListAsync(CancellationToken ct);
    Task<Organization?> GetAsync(Guid id, CancellationToken ct);
    Task<bool> CodeExistsAsync(string code, Guid? except, CancellationToken ct);
    Task<List<OrganizationMembership>> MembersAsync(Guid organizationId, CancellationToken ct);
    Task<OrganizationMembership?> MembershipAsync(Guid id, CancellationToken ct);
    Task<Guid[]> ActiveOrganizationsAsync(Guid userId, DateTimeOffset now, CancellationToken ct);
    void Add(Organization organization);
    void Add(OrganizationMembership membership);
    void Add(OrganizationAudit audit);
    Task SaveAsync(CancellationToken ct);
}
public interface IOrganizationTransaction : IAsyncDisposable { Task CommitAsync(CancellationToken ct); }
