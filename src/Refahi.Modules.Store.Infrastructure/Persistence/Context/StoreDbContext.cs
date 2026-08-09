using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Infrastructure.Persistence.Configurations;

namespace Refahi.Modules.Store.Infrastructure.Persistence.Context;

public class StoreDbContext : DbContext
{
    public StoreDbContext(DbContextOptions<StoreDbContext> options) : base(options) { }

    public DbSet<StoreModule> Modules => Set<StoreModule>();
    public DbSet<Shop> Shops => Set<Shop>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<ShopProduct> ShopProducts => Set<ShopProduct>();
    public DbSet<ShopProductVariant> ShopProductVariants => Set<ShopProductVariant>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<VariantAttribute> VariantAttributes => Set<VariantAttribute>();
    public DbSet<VariantAttributeValue> VariantAttributeValues => Set<VariantAttributeValue>();
    public DbSet<ProductVariantCombination> ProductVariantCombinations => Set<ProductVariantCombination>();
    public DbSet<ProductSpecification> ProductSpecifications => Set<ProductSpecification>();
    public DbSet<ProductSession> ProductSessions => Set<ProductSession>();
    public DbSet<Banner> Banners => Set<Banner>();
    public DbSet<DailyDeal> DailyDeals => Set<DailyDeal>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<StoreOrder> StoreOrders => Set<StoreOrder>();
    public DbSet<StoreOrderItem> StoreOrderItems => Set<StoreOrderItem>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<VoucherRedemption> VoucherRedemptions => Set<VoucherRedemption>();
    public DbSet<VoucherRefundOverride> VoucherRefundOverrides => Set<VoucherRefundOverride>();
    public DbSet<VoucherRefundOverrideAttempt> VoucherRefundOverrideAttempts => Set<VoucherRefundOverrideAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("store");

        modelBuilder.ApplyConfiguration(new StoreModuleConfiguration());
        modelBuilder.ApplyConfiguration(new ShopConfiguration());
        modelBuilder.ApplyConfiguration(new ProductConfiguration());
        modelBuilder.ApplyConfiguration(new OfferConfiguration());
        modelBuilder.ApplyConfiguration(new ProductImageConfiguration());
        modelBuilder.ApplyConfiguration(new ProductVariantConfiguration());
        modelBuilder.ApplyConfiguration(new VariantAttributeConfiguration());
        modelBuilder.ApplyConfiguration(new VariantAttributeValueConfiguration());
        modelBuilder.ApplyConfiguration(new ProductVariantCombinationConfiguration());
        modelBuilder.ApplyConfiguration(new ProductSpecificationConfiguration());
        modelBuilder.ApplyConfiguration(new ProductSessionConfiguration());
        modelBuilder.ApplyConfiguration(new BannerConfiguration());
        modelBuilder.ApplyConfiguration(new DailyDealConfiguration());
        modelBuilder.ApplyConfiguration(new ReviewConfiguration());
        modelBuilder.ApplyConfiguration(new CartConfiguration());
        modelBuilder.ApplyConfiguration(new CartItemConfiguration());
        modelBuilder.ApplyConfiguration(new StoreOrderConfiguration());
        modelBuilder.ApplyConfiguration(new StoreOrderItemConfiguration());
        modelBuilder.ApplyConfiguration(new VoucherConfiguration());
        modelBuilder.ApplyConfiguration(new VoucherRedemptionConfiguration());
        modelBuilder.ApplyConfiguration(new VoucherRefundOverrideConfiguration());
        modelBuilder.ApplyConfiguration(new VoucherRefundOverrideAttemptConfiguration());
        modelBuilder.ApplyConfiguration(new ShopProductConfiguration());
        modelBuilder.ApplyConfiguration(new ShopProductVariantConfiguration());
    }
}
