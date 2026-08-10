using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Refahi.Modules.Store.Application.Contracts.Offers;
using Refahi.Modules.Store.Application.Contracts.Products.V3;
using Refahi.Modules.Store.Application.Features.CatalogV3;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Services;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class CatalogV3DomainTests
{
    [Theory]
    [InlineData(10_000, 0, 10_000)]
    [InlineData(10_000, 12.5, 8_750)]
    [InlineData(10_000, 100, 0)]
    [InlineData(1, 50, 0)]
    [InlineData(3, 50, 1)]
    public void Offer_price_uses_single_away_from_zero_rounding(
        long original,
        decimal discount,
        long expected
    ) => Assert.Equal(expected, Offer.CalculateFinalPrice(original, discount));

    [Fact]
    public void Offer_rejects_invalid_window_and_price()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<StoreDomainException>(() =>
            Offer.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, 0, 0, now, null)
        );
        Assert.Throws<StoreDomainException>(() =>
            Offer.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, 100, 101, now, null)
        );
        Assert.Throws<StoreDomainException>(() =>
            Offer.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, 100, 0, now, now)
        );
    }

    [Fact]
    public void Offer_rejects_discount_scale_above_persisted_precision()
    {
        var now = DateTimeOffset.UtcNow;
        var ex = Assert.Throws<StoreDomainException>(() =>
            Offer.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, 10_000, 33.335m, now, null)
        );
        Assert.Equal("INVALID_DISCOUNT_SCALE", ex.ErrorCode);
        Assert.Equal(6_667, Offer.CalculateFinalPrice(10_000, 33.33m));
    }

    [Fact]
    public void Offer_validators_reject_discount_scale_above_two()
    {
        var now = DateTimeOffset.UtcNow;
        var create = new CreateOfferCommand(
            Guid.NewGuid(),
            false,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            10_000,
            33.335m,
            now,
            null
        );
        var update = new UpdateOfferCommand(
            Guid.NewGuid(),
            false,
            Guid.NewGuid(),
            10_000,
            33.335m,
            now,
            null,
            1
        );
        Assert.Contains(
            new CreateOfferValidator().Validate(create).Errors,
            x => x.ErrorMessage == "درصد تخفیف حداکثر می‌تواند دو رقم اعشار داشته باشد"
        );
        Assert.Contains(
            new UpdateOfferValidator().Validate(update).Errors,
            x => x.ErrorMessage == "درصد تخفیف حداکثر می‌تواند دو رقم اعشار داشته باشد"
        );
        Assert.True(
            new CreateOfferValidator().Validate(create with { DiscountPercent = 33.33m }).IsValid
        );
    }

    [Fact]
    public void Resolver_uses_bounded_nearest_end_then_latest_start()
    {
        var now = DateTimeOffset.UtcNow;
        var open = Offer.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            100,
            0,
            now.AddDays(-10),
            null
        );
        var later = Offer.Create(
            open.ProductId,
            open.ShopId,
            null,
            null,
            100,
            0,
            now.AddDays(-3),
            now.AddDays(3)
        );
        var winner = Offer.Create(
            open.ProductId,
            open.ShopId,
            null,
            null,
            100,
            0,
            now.AddDays(-1),
            now.AddDays(1)
        );
        open.Activate();
        later.Activate();
        winner.Activate();
        Assert.Same(winner, OfferResolver.Select([open, later, winner], now));
        winner.Deactivate();
        Assert.Same(later, OfferResolver.Select([open, later, winner], now));
        later.SoftDelete();
        Assert.Same(open, OfferResolver.Select([open, later, winner], now));
    }

    [Fact]
    public void Resolver_excludes_future_expired_inactive_and_deleted()
    {
        var now = DateTimeOffset.UtcNow;
        var at = now.AddHours(2);
        var p = Guid.NewGuid();
        var s = Guid.NewGuid();
        var future = Offer.Create(p, s, null, null, 100, 0, now.AddHours(3), null);
        future.Activate();
        var expired = Offer.Create(p, s, null, null, 100, 0, now.AddHours(-2), now.AddHours(1));
        expired.Activate();
        var inactive = Offer.Create(p, s, null, null, 100, 0, now.AddHours(-1), null);
        var deleted = Offer.Create(p, s, null, null, 100, 0, now.AddHours(-1), null);
        deleted.Activate();
        deleted.SoftDelete();
        Assert.Null(OfferResolver.Select([future, expired, inactive, deleted], at));
    }

    [Fact]
    public void Shop_channel_is_immutable()
    {
        var shop = Shop.Create("فروشگاه", "shop", ShopType.Online, Guid.NewGuid());
        var ex = Assert.Throws<StoreDomainException>(() =>
            shop.UpdateInfo(
                "فروشگاه",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                ShopType.InPerson
            )
        );
        Assert.Equal("SHOP_CHANNEL_IMMUTABLE", ex.ErrorCode);
        Assert.Equal(SalesChannel.Online, shop.Channel);
    }

    [Fact]
    public void Locked_enum_values_are_stable()
    {
        Assert.Equal(1, (short)ProductType.Goods);
        Assert.Equal(2, (short)ProductType.Service);
        Assert.Equal(1, (short)SalesModel.Unlimited);
        Assert.Equal(2, (short)SalesModel.InventoryBased);
        Assert.Equal(3, (short)SalesModel.SessionBased);
        Assert.Equal(1, (short)FulfillmentMethod.Pickup);
        Assert.Equal(4, (short)FulfillmentMethod.Download);
        Assert.Equal(1, (short)SalesChannel.Online);
        Assert.Equal(2, (short)SalesChannel.InPerson);
    }

    [Fact]
    public void Product_v3_boundary_rejects_unknown_enums()
    {
        var command = new CreateProductV3Command(
            Guid.NewGuid(),
            false,
            Guid.NewGuid(),
            1,
            99,
            99,
            99,
            99,
            "محصول",
            "product",
            null
        );
        var result = new CreateProductV3Validator().Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.ErrorMessage == "کانال فروش نامعتبر است");
        Assert.Contains(result.Errors, x => x.ErrorMessage == "نوع محصول نامعتبر است");
        Assert.Contains(result.Errors, x => x.ErrorMessage == "مدل فروش نامعتبر است");
        Assert.Contains(result.Errors, x => x.ErrorMessage == "روش تحویل نامعتبر است");
    }

    [Fact]
    public void Ef_model_has_offer_concurrency_constraints_and_nulls_not_distinct_unique_index()
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=x;Password=x")
            .Options;
        using var db = new StoreDbContext(options);
        var entity = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Offer))!;
        Assert.True(entity.FindProperty(nameof(Offer.Version))!.IsConcurrencyToken);
        var index = entity
            .GetIndexes()
            .Single(x => x.GetDatabaseName() == "UX_offers_open_coordinate");
        Assert.True(index.IsUnique);
        Assert.False(index.GetAreNullsDistinct());
        Assert.Equal("\"IsDeleted\" = false AND \"EndDateUtc\" IS NULL", index.GetFilter());
        Assert.Equal(3, entity.GetCheckConstraints().Count());
    }
}
