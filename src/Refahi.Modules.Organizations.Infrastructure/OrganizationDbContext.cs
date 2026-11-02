using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Organizations.Domain;
namespace Refahi.Modules.Organizations.Infrastructure;

public sealed class OrganizationDbContext(DbContextOptions<OrganizationDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMembership> Memberships => Set<OrganizationMembership>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema("organizations");
        b.Entity<OrganizationAudit>(e => { e.ToTable("audit"); e.HasKey(x => x.Id); e.HasIndex(x => x.SubjectId); e.Property(x => x.Action).HasMaxLength(80); });
        b.Entity<Organization>(e => { e.ToTable("organizations"); e.HasKey(x => x.Id); e.Property(x => x.Code).HasMaxLength(50); e.Property(x => x.Title).HasMaxLength(200); e.HasIndex(x => x.Code).IsUnique(); });
        b.Entity<OrganizationMembership>(e => { e.ToTable("memberships"); e.HasKey(x => x.Id); e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict); e.HasIndex(x => new { x.OrganizationId, x.UserId }); e.HasIndex(x => new { x.UserId, x.ValidFrom }); });
    }
}
public sealed class OrganizationStore(OrganizationDbContext db) : IOrganizationStore
{
    public async Task<IOrganizationTransaction> LockAsync(CancellationToken ct)
    {
        var tx = await db.Database.BeginTransactionAsync(ct);
        try { await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(170117, 1)", ct); return new Transaction(tx); }
        catch { await tx.DisposeAsync(); throw; }
    }
    public Task<List<Organization>> ListAsync(CancellationToken ct) => db.Organizations.OrderBy(x => x.Title).ToListAsync(ct);
    public Task<Organization?> GetAsync(Guid id, CancellationToken ct) => db.Organizations.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> CodeExistsAsync(string code, Guid? except, CancellationToken ct) => db.Organizations.AnyAsync(x => x.Code == code && x.Id != except, ct);
    public Task<List<OrganizationMembership>> MembersAsync(Guid id, CancellationToken ct) => db.Memberships.Where(x => x.OrganizationId == id).OrderByDescending(x => x.ValidFrom).ToListAsync(ct);
    public Task<OrganizationMembership?> MembershipAsync(Guid id, CancellationToken ct) => db.Memberships.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<Guid[]> ActiveOrganizationsAsync(Guid user, DateTimeOffset now, CancellationToken ct) =>
        (from m in db.Memberships
         join o in db.Organizations on m.OrganizationId equals o.Id
         where m.UserId == user && o.IsActive && m.RevokedAt == null && m.ValidFrom <= now && (m.ValidUntil == null || m.ValidUntil > now)
         select o.Id).Distinct().ToArrayAsync(ct);
    public void Add(Organization value) => db.Add(value);
    public void Add(OrganizationMembership value) => db.Add(value);
    public void Add(OrganizationAudit value) => db.Add(value);
    public async Task SaveAsync(CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
    }
    private sealed class Transaction(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx) : IOrganizationTransaction
    { public Task CommitAsync(CancellationToken ct) => tx.CommitAsync(ct); public ValueTask DisposeAsync() => tx.DisposeAsync(); }
}
