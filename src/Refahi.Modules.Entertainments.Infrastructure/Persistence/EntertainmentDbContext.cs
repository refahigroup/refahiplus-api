using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Entertainments.Domain;

namespace Refahi.Modules.Entertainments.Infrastructure.Persistence;

public sealed class EntertainmentDbContext(DbContextOptions<EntertainmentDbContext> options) : DbContext(options)
{
    public const string Schema = "entertainments";
    public DbSet<EntertainmentCart> Carts => Set<EntertainmentCart>();
    public DbSet<EntertainmentCartItem> CartItems => Set<EntertainmentCartItem>();
    public DbSet<EntertainmentPurchase> Orders => Set<EntertainmentPurchase>();
    public DbSet<EntertainmentPurchaseItem> OrderItems => Set<EntertainmentPurchaseItem>();
    public DbSet<ProviderFulfillment> Fulfillments => Set<ProviderFulfillment>();
    public DbSet<ProviderTicket> Tickets => Set<ProviderTicket>();
    public DbSet<ProviderOperationAttempt> OperationAttempts => Set<ProviderOperationAttempt>();
    public DbSet<EntertainmentCheckoutSession> CheckoutSessions => Set<EntertainmentCheckoutSession>();
    public DbSet<EntertainmentProviderReceipt> ProviderReceipts => Set<EntertainmentProviderReceipt>();

    public DbSet<TouristPanelProgram> Programs => Set<TouristPanelProgram>();
    public DbSet<TouristPanelSyncState> SyncStates => Set<TouristPanelSyncState>();
    public DbSet<TouristPanelLocationMapping> LocationMappings => Set<TouristPanelLocationMapping>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema(Schema);
        b.Entity<TouristPanelProgram>(x => {
            x.ToTable("touristpanel_programs"); x.HasKey(v => v.ProgramKey);
            x.HasIndex(v => new { v.AccountKey, v.NativeId }).IsUnique();
            x.HasIndex(v => new { v.AccountKey, v.IsListed, v.NextDetailAttemptUtc });
        });
        b.Entity<TouristPanelSyncState>(x => { x.ToTable("touristpanel_sync_states"); x.HasKey(v => v.AccountKey); });
        b.Entity<TouristPanelLocationMapping>(x => { x.ToTable("touristpanel_location_mappings"); x.HasKey(v => new { v.AccountKey, v.LocationCode }); });
        b.Entity<EntertainmentCheckoutSession>(x =>
        {
            x.ToTable("checkout_sessions"); x.HasKey(v => v.Id);
            x.SplitToTable("touristpanel_session_reservations", t => {
                t.Property(v => v.Id); t.Property(v => v.ReservationReference); t.Property(v => v.ReservationProtected);
                t.Property(v => v.ResolutionActorId); t.Property(v => v.ResolutionKey); t.Property(v => v.ResolutionFingerprint);
                t.Property(v => v.ResolutionEvidenceProtected); t.Property(v => v.ResolutionOutcome); t.Property(v => v.ResolvedAtUtc);
            });
            x.Property(v => v.Version).IsRowVersion();
            x.Property(v => v.ProviderKey).HasMaxLength(80);
            x.Property(v => v.IdempotencyKey).HasMaxLength(200);
            x.HasIndex(v => new { v.UserId, v.IdempotencyKey }).IsUnique();
            x.HasIndex(v => new { v.Status, v.PayableUntil });
        });
        b.Entity<EntertainmentProviderReceipt>(x =>
        { x.ToTable("touristpanel_receipts"); x.HasKey(v => new { v.AccountKey, v.OperationId }); });
        b.Entity<EntertainmentCart>(x =>
        {
            x.ToTable("carts"); x.HasKey(v => v.Id); x.HasIndex(v => v.UserId).IsUnique();
            x.Property(v => v.Version).IsRowVersion();
            x.HasMany(v => v.Items).WithOne().HasForeignKey(v => v.CartId).OnDelete(DeleteBehavior.Cascade);
            x.Navigation(v => v.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        b.Entity<EntertainmentCartItem>(x =>
        {
            x.ToTable("cart_items"); x.HasKey(v => v.Id);
            x.Property(v => v.ProviderKey).HasMaxLength(80); x.Property(v => v.SupplierKey).HasMaxLength(200);
            x.Property(v => v.ProgramKey).HasMaxLength(300); x.Property(v => v.OfferKey).HasMaxLength(300);
            x.Property(v => v.PurchaseOptionKey).HasMaxLength(80); x.Property(v => v.Title).HasMaxLength(500);
            x.Property(v => v.SupplierTitle).HasMaxLength(300); x.Property(v => v.ProgramTitle).HasMaxLength(500);
            x.Property(v => v.ProgramImageUrl).HasMaxLength(2000); x.Property(v => v.OptionTitle).HasMaxLength(300);
            x.Property(v => v.OfferTitle).HasMaxLength(500);
            x.HasIndex(v => new { v.CartId, v.ProviderKey, v.ProgramKey, v.OfferKey, v.PurchaseOptionKey }).IsUnique();
        });
        b.Entity<EntertainmentPurchase>(x =>
        {
            x.ToTable("purchases"); x.HasKey(v => v.Id);
            x.SplitToTable("touristpanel_reservations", t => {
                t.Property(v => v.Id); t.Property(v => v.ReservationReference);
                t.Property(v => v.ReservationContextProtected); t.Property(v => v.CartSelectionProtected);
            }); x.Property(v => v.Version).IsRowVersion();
            x.Property(v => v.IdempotencyKey).HasMaxLength(200); x.Property(v => v.RequestFingerprint).HasMaxLength(128);
            x.Property(v => v.RecipientNameProtected).HasMaxLength(4000); x.Property(v => v.RecipientMobileProtected).HasMaxLength(4000);
            x.HasIndex(v => new { v.UserId, v.IdempotencyKey }).IsUnique(); x.HasIndex(v => v.OrderId).IsUnique();
            x.HasMany(v => v.Items).WithOne().HasForeignKey(v => v.EntertainmentPurchaseId).OnDelete(DeleteBehavior.Cascade);
            x.HasMany(v => v.Fulfillments).WithOne().HasForeignKey(v => v.EntertainmentPurchaseId).OnDelete(DeleteBehavior.Cascade);
            x.Navigation(v => v.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
            x.Navigation(v => v.Fulfillments).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        b.Entity<EntertainmentPurchaseItem>(x =>
        {
            x.ToTable("purchase_items"); x.HasKey(v => v.Id);
            x.SplitToTable("touristpanel_purchase_item_contexts", t => { t.Property(v => v.Id); t.Property(v => v.ProviderPayloadJson); });
            x.Property(v => v.ProviderKey).HasMaxLength(80); x.Property(v => v.SupplierKey).HasMaxLength(200);
            x.Property(v => v.ProgramKey).HasMaxLength(300); x.Property(v => v.OfferKey).HasMaxLength(300);
            x.Property(v => v.PurchaseOptionKey).HasMaxLength(80); x.Property(v => v.Title).HasMaxLength(500);
            x.Property(v => v.OfferTitle).HasMaxLength(500); x.Property(v => v.CategoryCode).HasMaxLength(200);
            x.Property(v => v.ProviderPayloadJson).HasColumnType("jsonb");
        });
        b.Entity<ProviderFulfillment>(x =>
        {
            x.ToTable("touristpanel_issuances"); x.HasKey(v => v.Id); x.Property(v => v.ProviderKey).HasMaxLength(80);
            x.Property(v => v.ProviderOrderCode).HasMaxLength(300); x.Property(v => v.FailureReason).HasMaxLength(1000);
            x.HasIndex(v => new { v.EntertainmentPurchaseId, v.ProviderKey }).IsUnique();
            x.HasMany(v => v.Tickets).WithOne().HasForeignKey(v => v.ProviderFulfillmentId).OnDelete(DeleteBehavior.Cascade);
            x.HasMany(v => v.Attempts).WithOne().HasForeignKey(v => v.ProviderFulfillmentId).OnDelete(DeleteBehavior.Cascade);
            x.Navigation(v => v.Tickets).UsePropertyAccessMode(PropertyAccessMode.Field);
            x.Navigation(v => v.Attempts).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        b.Entity<ProviderTicket>(x =>
        {
            x.ToTable("touristpanel_tickets"); x.HasKey(v => v.Id); x.Property(v => v.CodeHash).HasMaxLength(128);
            x.Property(v => v.CodeProtected).HasMaxLength(4000); x.HasIndex(v => v.CodeHash).IsUnique();
        });
        b.Entity<ProviderOperationAttempt>(x =>
        {
            x.ToTable("touristpanel_operations"); x.HasKey(v => v.Id); x.Property(v => v.Operation).HasMaxLength(30);
            x.Property(v => v.IdempotencyKey).HasMaxLength(200); x.Property(v => v.PayloadHash).HasMaxLength(128);
            x.Property(v => v.SanitizedError).HasMaxLength(1000); x.HasIndex(v => new { v.ProviderFulfillmentId, v.IdempotencyKey }).IsUnique();
        });
        // Domain factories allocate GUIDs before children enter an already-tracked aggregate.
        // EF must insert these children rather than infer that a nonempty key already exists.
        foreach (var entity in b.Model.GetEntityTypes())
            foreach (var key in entity.FindPrimaryKey()?.Properties ?? [])
                if (key.ClrType == typeof(Guid)) key.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
    }
}
