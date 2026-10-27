using FluentValidation;
using MediatR;
using Refahi.Modules.Catalogue.Application;
using Refahi.Modules.Catalogue.Application.Contracts;
using Refahi.Modules.References.Application.Contracts.Dtos;
using Refahi.Modules.References.Application.Contracts.Queries;
using Xunit;

namespace Refahi.Modules.Catalogue.Tests;

public sealed class CatalogueLocationTests
{
    private static CatalogueItem Item(string source, string id, string title, params CatalogueLocation[] places) =>
        new(source+":"+id, source, source, id, title, "طنز ارکیده", null, null, null, null, [], "/detail", DateTimeOffset.UtcNow, false,
            Category:new(source,source), Locations:places);
    private static CatalogueLocation Place(string id, double? lat, int city=1, int province=1) =>
        new(id,province,"استان",city,"شهر",Address:"پیروزی",Point:lat.HasValue?new(lat.Value,0):null);
    private sealed class Source(CatalogueItem[] items) : ICatalogueSource
    {
        public Task<CatalogueSourceResult> SearchAsync(CatalogueQuery q,int take,CancellationToken ct)
        {
            var found=items.Where(x=>q.Mode is "all" or "nearby" || x.Locations!.Any(p=>CatalogueOrdering.Matches(p,q)))
                .Where(x=>CatalogueText.Normalize(q.Q).Split(' ',StringSplitOptions.RemoveEmptyEntries).All(w=>CatalogueText.Normalize(x.Title+" "+x.Summary+" "+string.Join(" ",x.Locations!.Select(p=>p.Address))).Contains(w)))
                .Select(x=>CatalogueOrdering.Prepare(x,q)).OrderBy(x=>x,CatalogueOrdering.Comparer).ToArray();
            return Task.FromResult(new CatalogueSourceResult(found.Take(take).ToArray(),found.Length,false));
        }
        public Task<IReadOnlyList<CatalogueCity>> CitiesAsync(CancellationToken ct)=>Task.FromResult<IReadOnlyList<CatalogueCity>>([new(1,"شهر","استان",1)]);
    }
    private sealed class Executor(Dictionary<string,CatalogueItem[]> data) : ICatalogueExecutor
    {
        public IReadOnlyList<string> Keys=>data.Keys.ToArray();
        public Task<T> ExecuteAsync<T>(string key,Func<ICatalogueSource,CancellationToken,Task<T>> action,CancellationToken ct)=>action(new Source(data[key]),ct);
    }
    private sealed class Geography : ISender
    {
        public Task<T> Send<T>(IRequest<T> request,CancellationToken ct=default) => Task.FromResult((T)(object)(request switch {
            GetProvincesQuery => new GetProvincesResponse([new(1,"استان","Province","province",0,true)]),
            GetCityByIdQuery q => q.Id == 1 ? new CityDto(1,"شهر","City","city",1,"استان",0,true) : null!,
            _ => throw new NotSupportedException()
        }));
        public Task<object?> Send(object request,CancellationToken ct=default)=>throw new NotSupportedException();
        public IAsyncEnumerable<T> CreateStream<T>(IStreamRequest<T> request,CancellationToken ct=default)=>throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request,CancellationToken ct=default)=>throw new NotSupportedException();
    }

    [Fact] public async Task NearbyPrefixesFindLateAlphabeticalItemAndMergeSecondPage()
    {
        var hotels=Enumerable.Range(1,30).Select(i=>Item("hotels",i.ToString(),i==30?"zz":"aa"+i,Place("hotel",31-i))).ToArray();
        var pools=new[]{Item("pools","1","pool",Place("pool",0.5)),Item("pools","2","unknown",Place("unknown",null))};
        var service=new CatalogueService(new Executor(new(){["hotels"]=hotels,["pools"]=pools}));
        var q=new CatalogueQuery(Location:"nearby",Latitude:0,Longitude:0,PageSize:2);
        var first=await service.SearchAsync(q,default);
        Assert.Equal(new[]{"pools:1","hotels:30"},first.Items.Select(x=>x.Id));
        var second=await service.SearchAsync(q with{Page=2},default);
        Assert.Equal(new[]{"hotels:29","hotels:28"},second.Items.Select(x=>x.Id));
        var last=await service.SearchAsync(q with{Page=16},default);
        Assert.Null(last.Items.Last().DistanceMeters);Assert.Equal(32,last.TotalCount);Assert.False(last.IsPartial);
    }
    [Fact] public async Task MultiLocationFilmHasOneCardAndNearestValidPlace()
    {
        var film=Item("cinema","1","گل‌های ارکیده",Place("unknown",null),Place("far",20),Place("near",0.01));
        var service=new CatalogueService(new Executor(new(){["cinema"]=[film]}));
        var result=await service.SearchAsync(new(Q:"طنز پیروزی",Location:"nearby",Latitude:0,Longitude:0),default);
        var item=Assert.Single(result.Items);Assert.Equal("near",item.DisplayLocation!.Id);Assert.InRange(item.DistanceMeters!.Value,1111,1113);Assert.Equal(1,result.TotalCount);
    }
    [Fact] public async Task ProvinceAndLegacyCityUseCanonicalGeography()
    {
        var service=new CatalogueService(new Executor(new(){["cinema"]=[Item("cinema","1","film",Place("elsewhere",1,2,2),Place("local",null))]}),new Geography());
        var result=await service.SearchAsync(new(CityId:1),default);
        Assert.Equal("local",Assert.Single(result.Items).DisplayLocation!.Id);
        Assert.Single((await service.SearchAsync(new(ProvinceId:1),default)).Items);
        await Assert.ThrowsAsync<ValidationException>(()=>service.SearchAsync(new(CityId:1,ProvinceId:2),default));
        await Assert.ThrowsAsync<ValidationException>(()=>service.SearchAsync(new(CityId:999),default));
    }
    [Fact] public void InvalidLocationCombinationsAreRejected()
    {
        var validator=new CatalogueQueryValidator();
        foreach(var q in new[]{new CatalogueQuery(Location:"nearby"),new(Location:"nearby",Latitude:double.NaN,Longitude:1),new(Location:"nearby",Latitude:91,Longitude:1),new(Location:"nearby",Latitude:0,Longitude:0,CityId:1),new(Location:"nearby",Latitude:0,Longitude:0,Sort:"title-asc"),new(Location:"province"),new(Location:"all",ProvinceId:1)})
            Assert.False(validator.Validate(q).IsValid);
        Assert.True(validator.Validate(new CatalogueQuery(Location:"nearby",Latitude:0,Longitude:0)).IsValid);
    }
    [Fact] public void DistanceHandlesPolesAntipodesInvalidAndAbsentPoints()
    {
        Assert.Equal(0d,CatalogueOrdering.Distance(new(0,0),new(0,0)));
        Assert.InRange(CatalogueOrdering.Distance(new(0,0),new(0,180))!.Value,20015000,20016000);
        Assert.Null(CatalogueOrdering.Distance(new(0,0),new(double.PositiveInfinity,0)));
        Assert.Null(CatalogueOrdering.Distance(null,new(0,0)));
    }
    [Fact] public async Task LocationFiltersMergeReferenceIds()
    {
        var result=await new CatalogueService(new Executor(new(){["hotels"]=[],["cinema"]=[]})).LocationsAsync(default);
        Assert.Single(result.Provinces);Assert.Single(result.Cities);Assert.False(result.IsPartial);
    }
}
