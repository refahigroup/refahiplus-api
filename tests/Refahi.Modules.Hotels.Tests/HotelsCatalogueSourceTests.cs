using Refahi.Modules.Catalogue.Application.Contracts;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
using Refahi.Modules.Hotels.Application.Features.Discovery;
using Refahi.Modules.Media.Application.Contracts.External;
using Xunit;
namespace Refahi.Modules.Hotels.Tests;
public class HotelsCatalogueSourceTests
{
 sealed class Reader: IHotelDiscoveryReader {
 public Guid Key{get;}=Guid.NewGuid();
 public Task<HotelDiscoveryPage> SearchAsync(string? q,int? city,int take,CancellationToken ct)=>Task.FromResult(new HotelDiscoveryPage([new(Key,1,"کیش","هرمزگان","SnappTrip","هتل","معرفی","نشانی",4,["https://supplier.test/photo"],[],null,DateTimeOffset.UtcNow,true)],null,true));
 public Task<HotelDiscoveryProfile?> ProfileAsync(Guid key,CancellationToken ct)=>throw new NotSupportedException();
 public Task<IReadOnlyList<HotelCityDto>> CitiesAsync(CancellationToken ct)=>Task.FromResult<IReadOnlyList<HotelCityDto>>([new(1,"کیش","Kish",1,"هرمزگان")]);
 }
 sealed class Media(bool fail):IExternalMediaService {
 public Task<string?> GetPublicUrlAsync(string url,ExternalMediaOrigin? origin=null,CancellationToken ct=default)=>throw new NotSupportedException();
 public Task<IReadOnlyList<string>> GetPublicUrlsAsync(IEnumerable<string> urls,ExternalMediaOrigin? origin=null,CancellationToken ct=default)=>fail?throw new IOException():Task.FromResult<IReadOnlyList<string>>(urls.Select(_=>"https://media.test/public").ToArray());
 }
 [Theory][InlineData(false)][InlineData(true)]public async Task DiscoveryPreservesIdentityPartialnessAndNeverLeaksSupplierImages(bool mediaFailure){
 var reader=new Reader();var result=await new HotelsCatalogueSource(reader,new Media(mediaFailure)).SearchAsync(new(),20,default);var item=Assert.Single(result.Items);
 Assert.Equal($"hotels:{reader.Key:N}",item.Id);Assert.True(result.IsPartial);Assert.Null(result.TotalCount);Assert.Null(item.Price);Assert.True(item.IsStale);Assert.Equal(mediaFailure?null:"https://media.test/public",item.ImageUrl);Assert.Equal($"/trip/hotels/{reader.Key}",item.DetailPath);
 }
}
