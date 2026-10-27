using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Refahi.Modules.Catalogue.Infrastructure;
using Refahi.Modules.Catalogue.Application.Contracts;
using Xunit;
namespace Refahi.Modules.Catalogue.Tests;
public sealed class CatalogueExecutorTests
{
 sealed class Source:ICatalogueSource,IDisposable {
 public Guid Id{get;}=Guid.NewGuid(); public bool Disposed{get;private set;}
 public void Dispose()=>Disposed=true;
 public Task<CatalogueSourceResult> SearchAsync(CatalogueQuery q,int take,CancellationToken ct)=>throw new NotSupportedException();
 public Task<IReadOnlyList<CatalogueCity>> CitiesAsync(CancellationToken ct)=>throw new NotSupportedException();
 }
 static CatalogueExecutor Executor(ServiceProvider provider)=>new(provider.GetRequiredService<IServiceScopeFactory>(),[new("test",typeof(Source))],Options.Create(new CatalogueOptions{SourceTimeoutSeconds=1}),NullLogger<CatalogueExecutor>.Instance);
 [Fact]public async Task EachInvocationUsesAndDisposesItsOwnScope(){
 using var provider=new ServiceCollection().AddScoped<Source>().BuildServiceProvider();var executor=Executor(provider);
 var a=await executor.ExecuteAsync("test",(s,t)=>Task.FromResult((Source)s),default);var b=await executor.ExecuteAsync("test",(s,t)=>Task.FromResult((Source)s),default);
 Assert.NotEqual(a.Id,b.Id);Assert.True(a.Disposed);Assert.True(b.Disposed);
 }
 [Fact]public async Task CooperativeTimeoutDoesNotBecomeAnEmptyResult(){
 using var provider=new ServiceCollection().AddScoped<Source>().BuildServiceProvider();var executor=Executor(provider);
 await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>executor.ExecuteAsync("test",async(s,t)=>{await Task.Delay(Timeout.Infinite,t);return 0;},default));
 }
 [Fact]public async Task CallerCancellationPropagates(){
 using var provider=new ServiceCollection().AddScoped<Source>().BuildServiceProvider();using var cts=new CancellationTokenSource();cts.Cancel();
 await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>Executor(provider).ExecuteAsync("test",(s,t)=>Task.FromResult(1),cts.Token));
 }
 [Fact]public void DuplicateSourcesFailRegistration(){using var provider=new ServiceCollection().BuildServiceProvider();Assert.Throws<ArgumentException>(()=>new CatalogueExecutor(provider.GetRequiredService<IServiceScopeFactory>(),[new("test",typeof(Source)),new("test",typeof(Source))],Options.Create(new CatalogueOptions()),NullLogger<CatalogueExecutor>.Instance));}
}
