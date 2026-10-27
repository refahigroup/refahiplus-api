using MediatR;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Catalogue.Api;
using Refahi.Modules.Catalogue.Application;
using Refahi.Modules.Catalogue.Application.Contracts;
using Xunit;
namespace Refahi.Modules.Catalogue.Tests;
public sealed class CatalogueEndpointTests
{
 sealed class Executor:ICatalogueExecutor {
 public IReadOnlyList<string> Keys=>["test"];
 public Task<T> ExecuteAsync<T>(string key,Func<ICatalogueSource,CancellationToken,Task<T>> action,CancellationToken ct)=>action(new Source(),ct);
 }
 sealed class Source:ICatalogueSource {
 public Task<CatalogueSourceResult> SearchAsync(CatalogueQuery q,int take,CancellationToken ct)=>Task.FromResult(new CatalogueSourceResult([],0,false));
 public Task<IReadOnlyList<CatalogueCity>> CitiesAsync(CancellationToken ct)=>Task.FromResult<IReadOnlyList<CatalogueCity>>([new(1,"کیش","هرمزگان")]);
 }
 [Fact]public async Task HttpContractValidatesQueriesAndReturnsUnifiedNoStoreResponses(){
 var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();builder.WebHost.UseUrls("http://127.0.0.1:0");
 builder.Services.AddMediatR(typeof(CatalogueService).Assembly);builder.Services.AddScoped<CatalogueService>();builder.Services.AddSingleton<ICatalogueExecutor,Executor>();builder.Services.Configure<CatalogueOptions>(o=>o.Enabled=true);
 await using var app=builder.Build();new CatalogueEndpoints().Map(app);await app.StartAsync();
 var url=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();using var http=new HttpClient{BaseAddress=new Uri(url)};
 using var valid=await http.GetAsync("/api/catalogue/items");Assert.Equal(HttpStatusCode.OK,valid.StatusCode);Assert.True(valid.Headers.CacheControl!.NoStore);
 using var body=JsonDocument.Parse(await valid.Content.ReadAsStringAsync());Assert.True(body.RootElement.TryGetProperty("data",out _)||body.RootElement.TryGetProperty("Data",out _));
 foreach(var query in new[]{"cityId=bad","sources=unknown","page=0","page=51&pageSize=20","sort=price","location=nearby","location=nearby&latitude=NaN&longitude=0","location=nearby&latitude=1&longitude=2&cityId=1"})Assert.Equal(HttpStatusCode.BadRequest,(await http.GetAsync("/api/catalogue/items?"+query)).StatusCode);
 Assert.Equal(HttpStatusCode.OK,(await http.GetAsync("/api/catalogue/filters/cities")).StatusCode);
 Assert.Equal(HttpStatusCode.OK,(await http.GetAsync("/api/catalogue/items?location=nearby&latitude=0&longitude=0")).StatusCode);
 Assert.Equal(HttpStatusCode.OK,(await http.GetAsync("/api/catalogue/filters/locations")).StatusCode);
 await app.StopAsync();
 }
}
