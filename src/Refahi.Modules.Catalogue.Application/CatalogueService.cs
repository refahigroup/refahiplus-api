using FluentValidation;
using MediatR;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.Catalogue.Application.Contracts;
using Refahi.Modules.Catalogue.Domain;
namespace Refahi.Modules.Catalogue.Application;
public sealed class CatalogueQueryValidator : AbstractValidator<CatalogueQuery>
{
 public CatalogueQueryValidator() {
 RuleFor(x=>x.Q).MaximumLength(100).WithMessage("عبارت جستجو طولانی است.");
 RuleFor(x=>x.CityId).GreaterThan(0).WithMessage("شهر معتبر نیست.");
 RuleFor(x=>x.ProvinceId).GreaterThan(0).WithMessage("استان معتبر نیست.");
 RuleFor(x=>x.Mode).Must(x=>new[]{"all","province","city","nearby"}.Contains(x)).WithMessage("موقعیت معتبر نیست.");
 RuleFor(x=>x).Must(x=>x.EffectiveSort == (x.Mode == "nearby" ? "distance-asc" : "title-asc")).WithMessage("ترتیب با موقعیت سازگار نیست.");
 RuleFor(x=>x).Must(x=>x.Mode switch {
 "all" => x.CityId is null && x.ProvinceId is null && x.Latitude is null && x.Longitude is null,
 "province" => x.ProvinceId.HasValue && x.CityId is null && x.Latitude is null && x.Longitude is null,
 "city" => x.CityId.HasValue && x.Latitude is null && x.Longitude is null,
 "nearby" => x.CityId is null && x.ProvinceId is null && x.Origin is { IsValid:true },
 _ => false }).WithMessage("پارامترهای موقعیت معتبر نیستند.");
 RuleFor(x=>x.Page).GreaterThan(0).WithMessage("صفحه معتبر نیست.");
 RuleFor(x=>x.PageSize).InclusiveBetween(1,50).WithMessage("اندازه صفحه معتبر نیست.");
 RuleFor(x=>x).Must(x=>(long)x.Page*x.PageSize<=1000).WithMessage("جستجو را محدودتر کنید."); }
}
public sealed class CatalogueService(ICatalogueExecutor executor, ISender? sender = null)
{
 public async Task<CatalogueResponse> SearchAsync(CatalogueQuery query,CancellationToken ct)
 {
 await new CatalogueQueryValidator().ValidateAndThrowAsync(query,ct);
 if (query.Mode is "province" or "city") {
 var provinces = (await sender!.Send(new GetProvincesQuery(ActiveOnly:true),ct)).Provinces;
 if (query.Mode == "city") {
 var city = await sender.Send(new GetCityByIdQuery(query.CityId!.Value),ct);
 if(city is null || !city.IsActive || (query.ProvinceId.HasValue && query.ProvinceId != city.ProvinceId)) throw new ValidationException("شهر معتبر نیست.");
 query = query with { ProvinceId = city.ProvinceId };
 }
 if(!provinces.Any(x=>x.Id == query.ProvinceId && x.IsActive)) throw new ValidationException("استان معتبر نیست.");
 }
 var keys=Select(query.Sources);
 var results=await Task.WhenAll(keys.Select(async key => {
 try { return (key,result:await executor.ExecuteAsync(key,(source,token)=>source.SearchAsync(query,query.Page*query.PageSize,token),ct)); }
 catch(OperationCanceledException) when(ct.IsCancellationRequested) { throw; }
 catch(ValidationException) { throw; }
 catch { return (key,result:(CatalogueSourceResult?)null); }
 }));
 if(results.All(x=>x.result is null)) throw new CatalogueUnavailableException();
 var partial=results.Any(x=>x.result is null || x.result.IsPartial);
 var items=results.Where(x=>x.result!=null).SelectMany(x=>x.result!.Items.Where(i=>i.Source==x.key && CatalogueIdentity.IsSafePath(i.DetailPath))).DistinctBy(x=>x.Id)
 .Select(x=>CatalogueOrdering.Prepare(x,query)).OrderBy(x=>x,CatalogueOrdering.Comparer).ToArray();
 int? total=partial || results.Any(x=>x.result?.TotalCount is null)?null:results.Sum(x=>x.result!.TotalCount!.Value);
 var skip=(query.Page-1)*query.PageSize;
 return new(items.Skip(skip).Take(query.PageSize).ToArray(),query.Page,query.PageSize,total,query.Page*query.PageSize<1000 && (total.HasValue?total>skip+query.PageSize:items.Length>=skip+query.PageSize),partial,results.Select(x=>new CatalogueSourceStatus(x.key,x.result is null?"unavailable":x.result.IsPartial?"partial":"ready")).ToArray());
 }
 public async Task<IReadOnlyList<CatalogueCity>> CitiesAsync(CancellationToken ct) => (await LocationsAsync(ct)).Cities;
 public async Task<CatalogueLocationsResponse> LocationsAsync(CancellationToken ct) {
 var results=await Task.WhenAll(executor.Keys.Select(async key=> {
 try { return (key, cities:await executor.ExecuteAsync(key,(s,t)=>s.CitiesAsync(t),ct)); }
 catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
 catch{return (key,cities:(IReadOnlyList<CatalogueCity>?)null);}}));
 if(results.All(x=>x.cities is null)) throw new CatalogueUnavailableException();
 var cities=results.Where(x=>x.cities!=null).SelectMany(x=>x.cities!).DistinctBy(x=>x.Id).OrderBy(x=>CatalogueText.Normalize(x.Name),CatalogueText.Comparer).ToArray();
 var provinces=cities.Where(x=>x.ProvinceId>0).DistinctBy(x=>x.ProvinceId).Select(x=>new CatalogueProvince(x.ProvinceId,x.ProvinceName)).OrderBy(x=>CatalogueText.Normalize(x.Name),CatalogueText.Comparer).ToArray();
 return new(provinces,cities,results.Any(x=>x.cities is null),results.Select(x=>new CatalogueSourceStatus(x.key,x.cities is null?"unavailable":"ready")).ToArray());
 }
 private string[] Select(string[]? selected) { var keys=selected is {Length:>0}?selected.Distinct().ToArray():executor.Keys.ToArray(); if(keys.Any(x=>!executor.Keys.Contains(x))) throw new ValidationException("منبع کاتالوگ معتبر نیست."); return keys; }
}
