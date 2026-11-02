using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Hotels.Domain.Aggregates.Catalogue;
namespace Refahi.Modules.Hotels.Infrastructure.Persistence.Configurations;

internal static class HotelCatalogueConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        var snappCities = model.Entity<SnappTripCityMapping>();
        snappCities.ToTable("snapptrip_city_mappings", t => t.ExcludeFromMigrations()); snappCities.HasKey(x => x.ProviderCityId);
        var lamasoCities = model.Entity<LamasoCityMapping>();
        lamasoCities.ToTable("lamaso_city_mappings", t => t.ExcludeFromMigrations()); lamasoCities.HasKey(x => x.ProviderCityId);
        var cities = model.Entity<HotelCityReference>(); cities.ToTable("catalogue_city_references"); cities.HasKey(x => x.Id);
        var inventory = model.Entity<HotelProviderInventory>(); inventory.ToTable("provider_inventory");
        inventory.HasKey(x => new { x.Provider, x.CityId }); inventory.Property(x => x.PayloadJson).HasColumnType("jsonb");
        cities.HasIndex(x => new { x.Provider, x.NativeId }).IsUnique(); cities.HasIndex(x => x.CityKey);
        var listings = model.Entity<HotelListing>(); listings.ToTable("catalogue_listings"); listings.HasKey(x => x.HotelKey);
        listings.HasIndex(x => new { x.Provider, x.NativeId }).IsUnique(); listings.HasIndex(x => x.ReferenceCityId);
        var offers = model.Entity<HotelOfferSnapshot>(); offers.ToTable("offer_snapshots"); offers.HasKey(x => x.Token);
        offers.Property(x => x.Token).HasMaxLength(64); offers.Property(x => x.OfferJson).HasColumnType("jsonb");
        offers.Property(x => x.TravelJson).HasColumnType("jsonb"); offers.HasIndex(x => x.ExpiresAtUtc);
        var reservations = model.Entity<HotelExternalReservation>(); reservations.ToTable("lamaso_reservations"); reservations.HasKey(x => x.HotelRequestId);
        reservations.HasIndex(x => new { x.Provider, x.ReferenceId }).IsUnique(); reservations.HasIndex(x => new { x.State, x.UpdatedAtUtc });
        reservations.Property(x => x.Version).IsConcurrencyToken(); reservations.Property(x => x.SubmitJson).HasColumnType("jsonb");
        reservations.Property(x => x.ResponseSnapshot).HasColumnType("jsonb");
        var operations = model.Entity<HotelExternalOperation>(); operations.ToTable("lamaso_operation_attempts"); operations.HasKey(x => x.Id);
        operations.HasIndex(x => new { x.HotelRequestId, x.StartedAtUtc }); operations.Property(x => x.Version).IsConcurrencyToken();
        operations.Property(x => x.ResponseSnapshot).HasColumnType("jsonb");
        operations.HasOne<HotelExternalReservation>().WithMany().HasForeignKey(x => x.HotelRequestId).OnDelete(DeleteBehavior.Restrict);
        reservations.HasIndex(x => new { x.NextRecoveryAtUtc, x.State });
    }
}
