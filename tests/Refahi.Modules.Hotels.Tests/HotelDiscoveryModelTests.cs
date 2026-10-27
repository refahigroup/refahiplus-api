using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Hotels.Infrastructure.Persistence;
using Refahi.Modules.Hotels.Domain.Aggregates.Catalogue;
using Xunit;
namespace Refahi.Modules.Hotels.Tests;
public class HotelDiscoveryModelTests
{
 [Fact]public void DiscoveryTablesAndQueriesUseProviderSchemasAndTranslate(){
 using var db=new HotelsDbContext(new DbContextOptionsBuilder<HotelsDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);
 Assert.Equal("snapptrip_discovery_hotels",db.Model.FindEntityType(typeof(SnappTripDiscoveryHotel))!.GetTableName());
 Assert.Equal("lamaso_discovery_hotels",db.Model.FindEntityType(typeof(LamasoDiscoveryHotel))!.GetTableName());
 IQueryable<HotelDiscovery> rows=db.Set<SnappTripDiscoveryHotel>();
 var sql=rows.Where(x=>x.Published&&x.SearchText.Contains("کیش")).OrderBy(x=>EF.Functions.Collate(x.SortTitle,"C")).Take(20).ToQueryString();
 Assert.Contains("snapptrip_discovery_hotels",sql);Assert.Contains("COLLATE",sql);
 Assert.False(db.Database.HasPendingModelChanges());
 }
 [Fact]public void RefreshFailurePreservesLastSuccess(){var state=SnappTripDiscoverySyncState.Create("1");var now=DateTimeOffset.UtcNow;state.Succeed(now,24);state.Fail(now.AddHours(1),15);Assert.Equal(now,state.LastSuccessAtUtc);Assert.Equal("failed",state.Status);}
 [Fact]public void RetiringPreservesHistoricalIdentity(){var key=Guid.NewGuid();var row=LamasoDiscoveryHotel.Create(key,"native","city");row.Publish(1,"هتل","","",4,"[]","[]",null,"هتل","هتل",DateTimeOffset.UtcNow);row.Retire();Assert.Equal(key,row.HotelKey);Assert.False(row.Published);}
}
