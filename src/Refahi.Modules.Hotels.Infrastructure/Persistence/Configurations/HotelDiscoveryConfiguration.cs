using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Hotels.Domain.Aggregates.Catalogue;
namespace Refahi.Modules.Hotels.Infrastructure.Persistence.Configurations;
internal static class HotelDiscoveryConfiguration
{
 public static void Configure(ModelBuilder model) {
 model.Ignore<HotelDiscovery>();model.Ignore<HotelDiscoverySyncState>();
 Hotel<SnappTripDiscoveryHotel>(model,"snapptrip_discovery_hotels");Hotel<LamasoDiscoveryHotel>(model,"lamaso_discovery_hotels");
 State<SnappTripDiscoverySyncState>(model,"snapptrip_discovery_sync_states");State<LamasoDiscoverySyncState>(model,"lamaso_discovery_sync_states");
 }
 private static void Hotel<T>(ModelBuilder model,string table) where T:HotelDiscovery {var e=model.Entity<T>();e.ToTable(table);e.HasKey(x=>x.HotelKey);e.HasIndex(x=>x.NativeId).IsUnique();e.HasIndex(x=>new{x.ReferenceCityId,x.Published,x.SortTitle});e.Property(x=>x.SortTitle).UseCollation("C");e.Property(x=>x.ContentJson).HasColumnType("jsonb");}
 private static void State<T>(ModelBuilder model,string table) where T:HotelDiscoverySyncState {var e=model.Entity<T>();e.ToTable(table);e.HasKey(x=>x.NativeCityId);}
}
