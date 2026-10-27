using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Catalogue.Application.Contracts;
namespace Refahi.Modules.Catalogue.Infrastructure;
public sealed class CatalogueExecutor(IServiceScopeFactory scopes, IEnumerable<CatalogueSourceRegistration> registrations, IOptions<CatalogueOptions> options, ILogger<CatalogueExecutor> logger) : ICatalogueExecutor
{
 private readonly Dictionary<string,Type> sources=registrations.ToDictionary(x=>x.Key,x=>x.Implementation,StringComparer.Ordinal);
 private readonly SemaphoreSlim gate=new(options.Value.MaxConcurrency);
 public IReadOnlyList<string> Keys=>sources.Keys.ToArray();
 public async Task<T> ExecuteAsync<T>(string key,Func<ICatalogueSource,CancellationToken,Task<T>> action,CancellationToken ct) {
 var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(options.Value.SourceTimeoutSeconds));
 var token=deadline.Token;
 try { await gate.WaitAsync(token); } catch {deadline.Dispose();throw;}
 return await Run().WaitAsync(token);
 async Task<T> Run() {
 await using var scope=scopes.CreateAsyncScope();
 var start=System.Diagnostics.Stopwatch.StartNew();
 try { return await action((ICatalogueSource)scope.ServiceProvider.GetRequiredService(sources[key]),token); }
 catch(Exception ex) when(!ct.IsCancellationRequested){logger.LogWarning("Catalogue source {Source} failed: {ErrorType}",key,ex.GetType().Name);throw;}
 finally {deadline.Dispose();gate.Release();logger.LogInformation("Catalogue source {Source} completed in {ElapsedMs} ms",key,start.ElapsedMilliseconds);}
 }
 }
}
