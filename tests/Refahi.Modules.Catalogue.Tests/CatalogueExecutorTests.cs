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
 [Fact]public void SourceAllowlistExcludesOtherModulesFromSearchAndLocationDiscovery(){
 using var provider=new ServiceCollection().BuildServiceProvider();
 var executor=new CatalogueExecutor(provider.GetRequiredService<IServiceScopeFactory>(),[new("hotels",typeof(Source)),new("entertainments",typeof(Source))],Options.Create(new CatalogueOptions{EnabledSources=["entertainments"]}),NullLogger<CatalogueExecutor>.Instance);
 Assert.Equal(new[]{"entertainments"},executor.Keys);
 }
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
 [Fact]public async Task CancellingAnInFlightSearchReleasesCapacityForTheNextSearch(){
 using var provider=new ServiceCollection().AddScoped<Source>().BuildServiceProvider();
 var executor=new CatalogueExecutor(provider.GetRequiredService<IServiceScopeFactory>(),[new("test",typeof(Source))],Options.Create(new CatalogueOptions{SourceTimeoutSeconds=5,MaxConcurrency=1}),NullLogger<CatalogueExecutor>.Instance);
 using var cancellation=new CancellationTokenSource();
 var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
 var pending=executor.ExecuteAsync("test",async(source,ct)=>{started.SetResult();await Task.Delay(Timeout.Infinite,ct);return 0;},cancellation.Token);
 await started.Task;
 cancellation.Cancel();
 await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pending);
 var next=await executor.ExecuteAsync("test",(source,ct)=>Task.FromResult(1),CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));
 Assert.Equal(1,next);
 }
 [Fact]public void DuplicateSourcesFailRegistration(){using var provider=new ServiceCollection().BuildServiceProvider();Assert.Throws<ArgumentException>(()=>new CatalogueExecutor(provider.GetRequiredService<IServiceScopeFactory>(),[new("test",typeof(Source)),new("test",typeof(Source))],Options.Create(new CatalogueOptions()),NullLogger<CatalogueExecutor>.Instance));}
 [Fact]public async Task TimedOutNonCooperativeWorkKeepsItsPermitUntilItsScopeIsDisposed(){
 using var provider=new ServiceCollection().AddScoped<Source>().BuildServiceProvider();
 var executor=new CatalogueExecutor(provider.GetRequiredService<IServiceScopeFactory>(),[new("test",typeof(Source))],Options.Create(new CatalogueOptions{SourceTimeoutSeconds=1,MaxConcurrency=1}),NullLogger<CatalogueExecutor>.Instance);
 var finish=new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
 Source? held=null;
 await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>executor.ExecuteAsync("test",(s,t)=>{held=(Source)s;return finish.Task;},default));
 Assert.NotNull(held);Assert.False(held.Disposed);
 var entered=false;using(var cancel=new CancellationTokenSource(100))
 await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>executor.ExecuteAsync("test",(s,t)=>{entered=true;return Task.FromResult(1);},cancel.Token));
 Assert.False(entered);
 finish.SetResult(1);
 Assert.Equal(2,await executor.ExecuteAsync("test",(s,t)=>Task.FromResult(2),default));
 Assert.True(held.Disposed);
 }
}
