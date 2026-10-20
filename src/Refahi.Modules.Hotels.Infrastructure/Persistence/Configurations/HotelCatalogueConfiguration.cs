using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Hotels.Domain.Aggregates.Catalogue;
namespace Refahi.Modules.Hotels.Infrastructure.Persistence.Configurations;

internal static class HotelCatalogueConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        var cities = model.Entity<HotelCityReference>(); cities.ToTable("catalogue_city_references"); cities.HasKey(x => x.Id);
        var inventory = model.Entity<HotelProviderInventory>(); inventory.ToTable("provider_inventory");
        inventory.HasKey(x => new { x.Provider, x.CityId }); inventory.Property(x => x.PayloadJson).HasColumnType("jsonb");
        cities.HasIndex(x => new { x.Provider, x.NativeId }).IsUnique(); cities.HasIndex(x => x.CityKey);
        var listings = model.Entity<HotelListing>(); listings.ToTable("catalogue_listings"); listings.HasKey(x => x.HotelKey);
        listings.HasIndex(x => new { x.Provider, x.NativeId }).IsUnique();
        var offers = model.Entity<HotelOfferSnapshot>(); offers.ToTable("offer_snapshots"); offers.HasKey(x => x.Token);
        offers.Property(x => x.Token).HasMaxLength(64); offers.Property(x => x.OfferJson).HasColumnType("jsonb");
        offers.Property(x => x.TravelJson).HasColumnType("jsonb"); offers.HasIndex(x => x.ExpiresAtUtc);
        var reservations = model.Entity<HotelExternalReservation>(); reservations.ToTable("external_reservations"); reservations.HasKey(x => x.HotelRequestId);
        reservations.HasIndex(x => new { x.Provider, x.ReferenceId }).IsUnique(); reservations.HasIndex(x => new { x.State, x.UpdatedAtUtc });
        reservations.Property(x => x.Version).IsConcurrencyToken(); reservations.Property(x => x.SubmitJson).HasColumnType("jsonb");
        reservations.Property(x => x.ResponseSnapshot).HasColumnType("jsonb");
    }
}
