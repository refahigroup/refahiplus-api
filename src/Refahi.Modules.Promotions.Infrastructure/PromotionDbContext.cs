using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.DataProtection;
using Refahi.Modules.Promotions.Domain;
using Refahi.Modules.Promotions.Application.Contracts;
namespace Refahi.Modules.Promotions.Infrastructure;

public sealed class PromotionDbContext(DbContextOptions<PromotionDbContext> options) : DbContext(options)
{
    public DbSet<PromotionCampaign> Campaigns => Set<PromotionCampaign>();
    public DbSet<PromotionCode> Codes => Set<PromotionCode>();
    public DbSet<PromotionRedemption> Redemptions => Set<PromotionRedemption>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema("promotions");
        b.Entity<PromotionCampaign>(e => { e.ToTable("campaigns"); e.HasKey(x => x.Id); e.Property(x => x.Title).HasMaxLength(200); e.Property(x => x.Percent).HasPrecision(8, 4); });
        b.Entity<PromotionCode>(e => { e.ToTable("codes"); e.HasKey(x => x.Id); e.Property(x => x.Hash).HasMaxLength(64); e.Property(x => x.BatchKey).HasMaxLength(200); e.HasIndex(x => x.Hash).IsUnique(); e.HasIndex(x => new { x.CampaignId, x.BatchKey }); e.HasOne<PromotionCampaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<PromotionRedemption>(e =>
        {
            e.ToTable("redemptions"); e.HasKey(x => x.Id); e.Ignore(x => x.OccupiesCapacity);
            e.Property(x => x.IdempotencyKey).HasMaxLength(200); e.Property(x => x.Fingerprint).HasMaxLength(64);
            e.HasIndex(x => new { x.OrderId, x.IdempotencyKey }).IsUnique();
            e.HasIndex(x => x.OrderId).IsUnique().HasFilter("\"Status\" IN (0,1,2)");
            e.HasIndex(x => new { x.CampaignId, x.Status, x.UserId });
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.HasOne<PromotionCampaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PromotionCode>().WithMany().HasForeignKey(x => x.CodeId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.RedemptionId);
            e.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        b.Entity<PromotionRedemptionItem>(e => { e.ToTable("redemption_items"); e.HasKey(x => x.Id); e.HasIndex(x => new { x.RedemptionId, x.OrderItemId }).IsUnique(); });
        b.Entity<PromotionAudit>(e => { e.ToTable("audit"); e.HasKey(x => x.Id); e.Property(x => x.Action).HasMaxLength(80); e.HasIndex(x => x.SubjectId); });
    }
}

public sealed class PromotionStore(PromotionDbContext db) : IPromotionStore
{
    public async Task<IPromotionTransaction> LockCampaignAsync(Guid id, CancellationToken ct)
    {
        var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(170116, hashtext({id.ToString()}))", ct);
            foreach (var entry in db.ChangeTracker.Entries().Where(x => x.State == EntityState.Unchanged).ToArray()) await entry.ReloadAsync(ct);
            return new Transaction(tx);
        }
        catch { await tx.DisposeAsync(); throw; }
    }
    public Task<PromotionCampaign?> CampaignAsync(Guid id, CancellationToken ct) => db.Campaigns.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<PromotionCode?> CodeByHashAsync(string hash, CancellationToken ct) => db.Codes.AsNoTracking().SingleOrDefaultAsync(x => x.Hash == hash, ct);
    public Task<PromotionCode?> CodeAsync(Guid id, CancellationToken ct) => db.Codes.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<PromotionRedemption?> RedemptionAsync(Guid id, CancellationToken ct) => db.Redemptions.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<PromotionRedemption?> OperationAsync(Guid order, string key, CancellationToken ct) => db.Redemptions.Include(x => x.Items).SingleOrDefaultAsync(x => x.OrderId == order && x.IdempotencyKey == key, ct);
    public async Task<PromotionUsage> UsageAsync(Guid campaign, Guid? user, Guid? code, CancellationToken ct)
    {
        var q = db.Redemptions.Where(x => x.CampaignId == campaign && (x.Status == RedemptionStatus.Reserved || x.Status == RedemptionStatus.PaymentPending || x.Status == RedemptionStatus.Committed));
        var stats = await q.GroupBy(x => x.CampaignId).Select(g => new { Budget = g.Sum(x => x.DiscountMinor), Reserved = g.Sum(x=>x.Status==RedemptionStatus.Committed?0:x.DiscountMinor), Consumed = g.Sum(x=>x.Status==RedemptionStatus.Committed?x.DiscountMinor:0), Uses = g.Count(), UserUses = g.Count(x => x.UserId == user), CodeUses = g.Count(x => x.CodeId == code) }).SingleOrDefaultAsync(ct);
        return stats == null ? new(0, 0, 0, 0) : new(stats.Budget, stats.Uses, stats.UserUses, stats.CodeUses,stats.Reserved,stats.Consumed);
    }
    public Task<List<PromotionCampaign>> CampaignsAsync(string? search, int skip, int take, CancellationToken ct) => db.Campaigns.Where(x => search == null || x.Title.Contains(search)).OrderByDescending(x => x.StartsAt).ThenBy(x => x.Id).Skip(skip).Take(take).ToListAsync(ct);
    public Task<List<PromotionCode>> CodesAsync(Guid id, int skip, int take, CancellationToken ct) => db.Codes.Where(x => x.CampaignId == id).OrderBy(x => x.Id).Skip(skip).Take(take).ToListAsync(ct);
    public Task<List<PromotionCode>> BatchCodesAsync(Guid id, string key, bool exact, CancellationToken ct) => db.Codes.Where(x => x.CampaignId == id && (exact ? x.BatchKey == key : x.BatchKey.StartsWith(key))).OrderBy(x => x.Id).Take(1000).ToListAsync(ct);
    public Task<List<PromotionRedemption>> RedemptionsAsync(Guid? order, Guid? user, RedemptionStatus? status, int skip, int take, CancellationToken ct) =>
        db.Redemptions.Where(x => (order == null || x.OrderId == order) && (user == null || x.UserId == user) && (status == null || x.Status == status)).OrderByDescending(x => x.CreatedAt).Skip(skip).Take(take).ToListAsync(ct);
    public Task<List<PromotionRedemption>> PendingAsync(int take, CancellationToken ct) => db.Redemptions.Include(x => x.Items).Where(x => x.Status == RedemptionStatus.Reserved || x.Status == RedemptionStatus.PaymentPending).OrderBy(x => x.CreatedAt).Take(take).ToListAsync(ct);
    public void Add(PromotionCampaign x) => db.Add(x);
    public void Add(PromotionCode x) => db.Add(x);
    public void Add(PromotionRedemption x) => db.Add(x);
    public void Add(PromotionAudit x) => db.Add(x);
    public async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        { throw new PromotionException("کد یا رزرو هم‌زمان ثبت شده است؛ وضعیت سفارش را دوباره بررسی کنید", 409); }
    }
    private sealed class Transaction(IDbContextTransaction tx) : IPromotionTransaction
    { public Task CommitAsync(CancellationToken ct) => tx.CommitAsync(ct); public ValueTask DisposeAsync() => tx.DisposeAsync(); }
}

public sealed class PromotionCodeProtector(IDataProtectionProvider provider) : IPromotionCodeProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("Refahi.Promotions.Codes.v1");
    public string Protect(string code) => _protector.Protect(code);
    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}
