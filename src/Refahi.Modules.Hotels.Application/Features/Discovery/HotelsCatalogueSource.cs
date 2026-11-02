using Refahi.Modules.Catalogue.Application.Contracts;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
using Refahi.Modules.Media.Application.Contracts.External;
namespace Refahi.Modules.Hotels.Application.Features.Discovery;
public sealed class HotelsCatalogueSource(IHotelDiscoveryReader reader,IExternalMediaService media) : ICatalogueSource
{
 public async Task<CatalogueSourceResult> SearchAsync(CatalogueQuery query,int take,CancellationToken ct) {
 var page=await reader.SearchAsync(new HotelDiscoverySearch(query.Q,query.CityId,query.ProvinceId,query.Origin?.Latitude,query.Origin?.Longitude),take,ct);
 var items=new List<CatalogueItem>();
 var imageReady=new HashSet<string>();
 foreach(var group in page.Items.Where(x=>!x.IsLocalContent).GroupBy(x=>x.Provider)) {
 try {await media.GetPublicUrlsAsync(group.SelectMany(x=>x.Images.Take(1)),new("Hotels",group.Key),ct);imageReady.Add(group.Key);}
 catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}catch { }
 }

 foreach(var hotel in page.Items) {
 IReadOnlyList<string> images=hotel.IsLocalContent?hotel.Images:[];
 try {if(!hotel.IsLocalContent && imageReady.Contains(hotel.Provider))images=await media.GetPublicUrlsAsync(hotel.Images.Take(1),new("Hotels",hotel.Provider),ct);} catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}catch { }
 items.Add(new($"hotels:{hotel.HotelKey:N}","hotels","hotel",hotel.HotelKey.ToString("N"),hotel.Title,HotelDiscoveryText.Summary(hotel.Description),images.FirstOrDefault(),hotel.ReferenceCityId,hotel.CityName,hotel.ProvinceName,hotel.Stars > 0 ? [hotel.Provider,$"{hotel.Stars} ستاره"] : [hotel.Provider],$"/trip/hotels/{hotel.HotelKey}",hotel.ObservedAtUtc,hotel.IsStale,Category:new("hotel","هتل"),
 Attributes:hotel.Stars > 0 ? [new("provider","ارائه‌دهنده",hotel.Provider),new("grade","درجه",$"{hotel.Stars} ستاره")] : [new("provider","ارائه‌دهنده",hotel.Provider)],
 Locations:[new(hotel.HotelKey.ToString("N"),hotel.ProvinceId,hotel.ProvinceName,hotel.ReferenceCityId,hotel.CityName,hotel.Title,hotel.Address,
 hotel.Latitude.HasValue && hotel.Longitude.HasValue ? new(hotel.Latitude.Value,hotel.Longitude.Value) : null)]));
 }
 return new(items,page.TotalCount,page.IsPartial);
 }
 public async Task<IReadOnlyList<CatalogueCity>> CitiesAsync(CancellationToken ct)=>(await reader.CitiesAsync(ct)).Select(x=>new CatalogueCity(x.ReferenceCityId,x.Name,x.ProvinceName,x.ProvinceId)).ToArray();
}
