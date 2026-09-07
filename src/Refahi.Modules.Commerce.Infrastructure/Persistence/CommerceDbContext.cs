using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Commerce.Domain;

namespace Refahi.Modules.Commerce.Infrastructure.Persistence;

public sealed class CommerceDbContext(DbContextOptions<CommerceDbContext> options) : DbContext(options)
{
    public const string Schema = "commerce";
    public DbSet<CommerceCart> Carts => Set<CommerceCart>();
    public DbSet<CommerceCartItem> CartItems => Set<CommerceCartItem>();
    public DbSet<CommerceOrder> Orders => Set<CommerceOrder>();
    public DbSet<CommerceOrderItem> OrderItems => Set<CommerceOrderItem>();
    public DbSet<ProviderFulfillment> Fulfillments => Set<ProviderFulfillment>();
    public DbSet<ProviderTicket> Tickets => Set<ProviderTicket>();
    public DbSet<ProviderOperationAttempt> OperationAttempts => Set<ProviderOperationAttempt>();
    public DbSet<CommerceCheckoutSession> CheckoutSessions => Set<CommerceCheckoutSession>();
    public DbSet<CommerceCatalogSnapshot> CatalogSnapshots => Set<CommerceCatalogSnapshot>();
    public DbSet<CommerceProviderReceipt> ProviderReceipts => Set<CommerceProviderReceipt>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema(Schema);
        b.Entity<CommerceCheckoutSession>(x =>
        {
            x.ToTable("checkout_sessions"); x.HasKey(v => v.Id);
            x.Property(v => v.Version).IsRowVersion();
            x.Property(v => v.ProviderKey).HasMaxLength(80);
            x.Property(v => v.IdempotencyKey).HasMaxLength(200);
            x.HasIndex(v => new { v.UserId, v.IdempotencyKey }).IsUnique();
            x.HasIndex(v => new { v.Status, v.PayableUntil });
        });
        b.Entity<CommerceCatalogSnapshot>(x =>
        { x.ToTable("catalog_snapshots"); x.HasKey(v => v.AccountKey); x.Property(v => v.PayloadJson).HasColumnType("jsonb"); });
        b.Entity<CommerceProviderReceipt>(x =>
        { x.ToTable("provider_receipts"); x.HasKey(v => new { v.AccountKey, v.OperationId }); });
        b.Entity<CommerceCart>(x =>
        {
            x.ToTable("carts"); x.HasKey(v => v.Id); x.HasIndex(v => v.UserId).IsUnique();
            x.Property(v => v.Version).IsRowVersion();
            x.HasMany(v => v.Items).WithOne().HasForeignKey(v => v.CartId).OnDelete(DeleteBehavior.Cascade);
            x.Navigation(v => v.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        b.Entity<CommerceCartItem>(x =>
        {
            x.ToTable("cart_items"); x.HasKey(v => v.Id);
            x.Property(v => v.ProviderKey).HasMaxLength(80); x.Property(v => v.SellerKey).HasMaxLength(200);
            x.Property(v => v.ProductKey).HasMaxLength(300); x.Property(v => v.OfferKey).HasMaxLength(300);
            x.Property(v => v.PurchaseOptionKey).HasMaxLength(80); x.Property(v => v.Title).HasMaxLength(500);
            x.Property(v => v.SellerTitle).HasMaxLength(300); x.Property(v => v.ProductTitle).HasMaxLength(500);
            x.Property(v => v.ProductImageUrl).HasMaxLength(2000); x.Property(v => v.OptionTitle).HasMaxLength(300);
            x.Property(v => v.OfferTitle).HasMaxLength(500);
            x.HasIndex(v => new { v.CartId, v.ProviderKey, v.ProductKey, v.OfferKey, v.PurchaseOptionKey }).IsUnique();
        });
        b.Entity<CommerceOrder>(x =>
        {
            x.ToTable("orders"); x.HasKey(v => v.Id); x.Property(v => v.Version).IsRowVersion();
            x.Property(v => v.IdempotencyKey).HasMaxLength(200); x.Property(v => v.RequestFingerprint).HasMaxLength(128);
            x.Property(v => v.RecipientNameProtected).HasMaxLength(4000); x.Property(v => v.RecipientMobileProtected).HasMaxLength(4000);
            x.HasIndex(v => new { v.UserId, v.IdempotencyKey }).IsUnique(); x.HasIndex(v => v.OrderId).IsUnique();
            x.HasMany(v => v.Items).WithOne().HasForeignKey(v => v.CommerceOrderId).OnDelete(DeleteBehavior.Cascade);
            x.HasMany(v => v.Fulfillments).WithOne().HasForeignKey(v => v.CommerceOrderId).OnDelete(DeleteBehavior.Cascade);
            x.Navigation(v => v.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
            x.Navigation(v => v.Fulfillments).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        b.Entity<CommerceOrderItem>(x =>
        {
            x.ToTable("order_items"); x.HasKey(v => v.Id);
            x.Property(v => v.ProviderKey).HasMaxLength(80); x.Property(v => v.SellerKey).HasMaxLength(200);
            x.Property(v => v.ProductKey).HasMaxLength(300); x.Property(v => v.OfferKey).HasMaxLength(300);
            x.Property(v => v.PurchaseOptionKey).HasMaxLength(80); x.Property(v => v.Title).HasMaxLength(500);
            x.Property(v => v.OfferTitle).HasMaxLength(500); x.Property(v => v.CategoryCode).HasMaxLength(200);
            x.Property(v => v.ProviderPayloadJson).HasColumnType("jsonb");
        });
        b.Entity<ProviderFulfillment>(x =>
        {
            x.ToTable("provider_fulfillments"); x.HasKey(v => v.Id); x.Property(v => v.ProviderKey).HasMaxLength(80);
            x.Property(v => v.ProviderOrderCode).HasMaxLength(300); x.Property(v => v.FailureReason).HasMaxLength(1000);
            x.HasIndex(v => new { v.CommerceOrderId, v.ProviderKey }).IsUnique();
            x.HasMany(v => v.Tickets).WithOne().HasForeignKey(v => v.ProviderFulfillmentId).OnDelete(DeleteBehavior.Cascade);
            x.HasMany(v => v.Attempts).WithOne().HasForeignKey(v => v.ProviderFulfillmentId).OnDelete(DeleteBehavior.Cascade);
            x.Navigation(v => v.Tickets).UsePropertyAccessMode(PropertyAccessMode.Field);
            x.Navigation(v => v.Attempts).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        b.Entity<ProviderTicket>(x =>
        {
            x.ToTable("provider_tickets"); x.HasKey(v => v.Id); x.Property(v => v.CodeHash).HasMaxLength(128);
            x.Property(v => v.CodeProtected).HasMaxLength(4000); x.HasIndex(v => v.CodeHash).IsUnique();
        });
        b.Entity<ProviderOperationAttempt>(x =>
        {
            x.ToTable("provider_operation_attempts"); x.HasKey(v => v.Id); x.Property(v => v.Operation).HasMaxLength(30);
            x.Property(v => v.IdempotencyKey).HasMaxLength(200); x.Property(v => v.PayloadHash).HasMaxLength(128);
            x.Property(v => v.SanitizedError).HasMaxLength(1000); x.HasIndex(v => new { v.ProviderFulfillmentId, v.IdempotencyKey }).IsUnique();
        });
    }
}
