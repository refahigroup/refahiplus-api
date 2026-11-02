using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
using Refahi.Modules.Hotels.Infrastructure.Workers;
using Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Api;
using Refahi.Modules.Hotels.Infrastructure.Providers.Lamasoo;
using Xunit;
namespace Refahi.Modules.Hotels.Tests;
public sealed class HotelDiscoveryInventoryTests
{
 sealed class Handler(Func<Uri,string> response):HttpMessageHandler {
 public List<Uri> Calls {get;}=[];
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){ct.ThrowIfCancellationRequested();Calls.Add(request.RequestUri!);return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(response(request.RequestUri!),System.Text.Encoding.UTF8,"application/json")});}
 }
 static HotelDiscoverySynchronizer Sync(Handler handler){var http=new HttpClient(handler){BaseAddress=new Uri("https://fixture.test/")};return new(null!,null!,null!,null!,new SnappTripApiClient(http,NullLogger<SnappTripApiClient>.Instance),new LamasooApiClient(http,NullLogger<LamasooApiClient>.Instance),Options.Create(new HotelDiscoveryOptions()),NullLogger<HotelDiscoverySynchronizer>.Instance);}
 [Fact]public async Task SnappTripDetailsUseBatchesOfTenAndNeverAvailability(){
 var handler=new Handler(uri=>{
 if(uri.AbsolutePath.StartsWith("/cities"))return uri.Query.Contains("offset=0")?JsonSerializer.Serialize(Enumerable.Range(1,11).Select(id=>new{id,name="هتل",city_id=1})):"[]";
 var ids=Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query)["id"].ToString().Split(',').Select(int.Parse);
 return JsonSerializer.Serialize(ids.Select(id=>new{hotel=new{id,title="هتل "+id,title_en="Hotel "+id,city=new{id=1},stars=4,enabled=true}}));
 });
 var hotels=await Sync(handler).Fetch("SnappTrip","1",default);Assert.Equal(11,hotels.Count);Assert.Equal(2,handler.Calls.Count(x=>x.AbsolutePath=="/hotels/"));Assert.DoesNotContain(handler.Calls,x=>x.AbsolutePath.Contains("availability"));Assert.Equal("Hotel 1",hotels[0].EnglishName);
 }
 [Theory]
 [InlineData("{}",null,null)]
 [InlineData("null",null,null)]
 [InlineData("\"missing\"",null,null)]
 [InlineData("{\"lat\":\"bad\",\"lon\":51}",null,null)]
 [InlineData("{\"lat\":0,\"lon\":0}",null,null)]
 [InlineData("{\"lat\":91,\"lon\":51}",null,null)]
 [InlineData("{\"lat\":35.7,\"lon\":51.4}",35.7,51.4)]
 [InlineData("{\"lat\":0,\"lon\":51}",0d,51d)]
 public async Task OptionalCoordinatesNeverDropValidHotels(string location,double? latitude,double? longitude){
 var handler=new Handler(uri=>uri.AbsolutePath.StartsWith("/cities") ? uri.Query.Contains("offset=0")?"[{\"id\":1,\"city_id\":1}]":"[]" : "[{\"hotel\":{\"id\":1,\"title\":\"Hotel\",\"city\":{\"id\":1},\"enabled\":true,\"location\":"+location+"}}]");
 var hotel=Assert.Single(await Sync(handler).Fetch("SnappTrip","1",default));Assert.Equal(latitude,hotel.Latitude);Assert.Equal(longitude,hotel.Longitude);
 }
 [Fact]public void DiscoverySummaryStripsMarkupAndHiddenScripts(){Assert.Equal("هتل ارکیده & تهران",HotelDiscoveryText.Summary("<p>هتل <b>ارکیده</b> &amp; تهران</p><script>alert(1)</script>"));}
 [Fact]public async Task RepeatedSnappTripPageCannotPublishPartialInventory(){var h=new Handler(_=>"""[{"id":1,"city_id":1,"name":"هتل"}]""");await Assert.ThrowsAsync<InvalidOperationException>(()=>Sync(h).Fetch("SnappTrip","1",default));Assert.Equal(2,h.Calls.Count);}
 [Fact]public async Task IncompleteSnappTripDetailsAreRejected(){var h=new Handler(u=>u.AbsolutePath.StartsWith("/cities")&&u.Query.Contains("offset=0")?"""[{"id":1,"city_id":1,"name":"هتل"}]""":"[]");await Assert.ThrowsAsync<InvalidOperationException>(()=>Sync(h).Fetch("SnappTrip","1",default));}
 [Fact]public async Task LamasooUsesOnlyInventoryAndKeepsNoRates(){var h=new Handler(_=>"""{"hotels":[{"id":"h1","city":"1","name":"هتل","star":4,"images":[],"room_rates":[{}]}]}""");var hotel=Assert.Single(await Sync(h).Fetch("Lamasoo","1",default));Assert.Equal("h1",hotel.Id);Assert.Single(h.Calls);Assert.Contains("hotel_inventory",h.Calls[0].AbsolutePath);}
 [Fact]public async Task InvalidLamasooCityIsRejected(){var h=new Handler(_=>"""{"hotels":[{"id":"h1","city":"wrong","name":"هتل","star":4}]}""");await Assert.ThrowsAsync<InvalidOperationException>(()=>Sync(h).Fetch("Lamasoo","1",default));}
 [Fact]public async Task CancelledInventoryRequestPropagatesCancellation(){using var cts=new CancellationTokenSource();cts.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>Sync(new Handler(_=>"[]")).Fetch("SnappTrip","1",cts.Token));}
}
