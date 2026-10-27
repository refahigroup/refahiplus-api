using Refahi.Modules.Catalogue.Application;
using Refahi.Modules.Catalogue.Application.Contracts;
using Refahi.Modules.Catalogue.Domain;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Catalogue.Infrastructure;
namespace Refahi.Modules.Catalogue.Tests;
public class CatalogueTests
{
 static CatalogueItem Item(string source,int n)=>new(source+":"+n,source,"test",n.ToString(),n.ToString("D3"),null,null,1,"شهر","استان",[],"/detail",DateTimeOffset.UtcNow,false);
 sealed class Executor(Dictionary<string,CatalogueSourceResult?> data):ICatalogueExecutor {
 public IReadOnlyList<string> Keys=>data.Keys.ToArray();
 public Task<T> ExecuteAsync<T>(string key,Func<ICatalogueSource,CancellationToken,Task<T>> action,CancellationToken ct)=>action(new Source(data[key]),ct);
 }
 sealed class Source(CatalogueSourceResult? result):ICatalogueSource {
 public Task<CatalogueSourceResult> SearchAsync(CatalogueQuery q,int take,CancellationToken ct)=>result is null?throw new IOException():Task.FromResult(result with{Items=result.Items.Take(take).ToArray()});
 public Task<IReadOnlyList<CatalogueCity>> CitiesAsync(CancellationToken ct)=>Task.FromResult<IReadOnlyList<CatalogueCity>>([]);
 }
 [Fact] public void SchemaModelMatchesSnapshot(){using var db=new CatalogueDbContext(new DbContextOptionsBuilder<CatalogueDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);Assert.False(db.Database.HasPendingModelChanges());}
 [Fact]public async Task GlobalSecondPageUsesPrefixesFromEverySource(){
 var service=new CatalogueService(new Executor(new(){["a"]=new(Enumerable.Range(1,8).Select(x=>Item("a",x)).ToArray(),8,false),["b"]=new(Enumerable.Range(10,8).Select(x=>Item("b",x)).ToArray(),8,false)}));
 var page=await service.SearchAsync(new(Page:2,PageSize:3),default);
 Assert.Equal(new[]{"004","005","006"},page.Items.Select(x=>x.Title));Assert.Equal(16,page.TotalCount);
 }
 [Fact]public async Task PartialSourceSuppressesTotal(){var page=await new CatalogueService(new Executor(new(){["a"]=new([Item("a",1)],1,true),["b"]=null})).SearchAsync(new(),default);Assert.True(page.IsPartial);Assert.Null(page.TotalCount);Assert.Single(page.Items);}
 [Fact]public async Task AllFailuresAreUnavailable(){await Assert.ThrowsAsync<CatalogueUnavailableException>(()=>new CatalogueService(new Executor(new(){["a"]=null})).SearchAsync(new(),default));}
 [Fact]public async Task EmptyReadySourceIsSuccessful(){var p=await new CatalogueService(new Executor(new(){["a"]=new([],0,false)})).SearchAsync(new(),default);Assert.Empty(p.Items);Assert.Equal(0,p.TotalCount);}
 [Theory][InlineData(0,20)][InlineData(1,51)][InlineData(51,20)]public void InvalidWindowsAreRejected(int page,int size)=>Assert.False(new CatalogueQueryValidator().Validate(new CatalogueQuery(Page:page,PageSize:size)).IsValid);
 [Fact]public void PersianNormalizationAndSafeTargets(){Assert.Equal("کیش 12",CatalogueText.Normalize("  كيش‌۱۲ "));Assert.False(CatalogueIdentity.IsSafePath("//evil.test"));Assert.False(CatalogueIdentity.IsSafePath("/\\evil"));}
}
