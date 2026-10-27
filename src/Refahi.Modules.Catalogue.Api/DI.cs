using Microsoft.EntityFrameworkCore;
using Refahi.Shared.Extensions;
using Refahi.Shared.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using FluentValidation;
using MediatR;
using Refahi.Modules.Catalogue.Application;
using Refahi.Modules.Catalogue.Application.Contracts;
using Refahi.Modules.Catalogue.Infrastructure;
using Refahi.Shared.Presentation;
namespace Refahi.Modules.Catalogue.Api;
public static class DI
{
 public static IServiceCollection RegisterCatalogueModule(this IServiceCollection services,IConfiguration config) {
 services.AddMediatR(typeof(CatalogueService).Assembly);
 services.AddOptions<CatalogueOptions>().Bind(config.GetSection("Catalogue")).Validate(x=>x.SourceTimeoutSeconds>0 && x.TimeoutSeconds>0 && x.MaxConcurrency>0).ValidateOnStart();
 services.AddDbContext<CatalogueDbContext>(o=>o.UseNpgsql(config.GetConnectionString(),n=>n.MigrationsHistoryTable("__EFMigrationsHistory","catalogue")));
 services.AddSingleton<ICatalogueExecutor,CatalogueExecutor>();services.AddScoped<CatalogueService>();return services; }
 public static WebApplication UseCatalogueModule(this WebApplication app) {
 _=app.Services.GetRequiredService<ICatalogueExecutor>();
 using(var scope=app.Services.CreateScope())scope.ServiceProvider.GetRequiredService<IDbTools>().ApplyMigrations<CatalogueDbContext>();
 new CatalogueEndpoints().Map(app);return app; }
}
public sealed class CatalogueEndpoints : IEndpoint
{
 private static readonly System.Diagnostics.Metrics.Meter Metrics = new("Refahi.Catalogue");
 private static readonly System.Diagnostics.Metrics.Counter<long> Responses = Metrics.CreateCounter<long>("catalogue.responses");
 private static readonly System.Diagnostics.Metrics.Histogram<double> Duration = Metrics.CreateHistogram<double>("catalogue.duration", "ms");
 public void Map(object app) {
 if(app is not Microsoft.AspNetCore.Routing.IEndpointRouteBuilder routes)return;
 var group=routes.MapGroup("/api/catalogue");
 group.MapGet("/items",async(HttpContext ctx,ISender sender,IOptions<CatalogueOptions> options,CancellationToken ct)=> await Execute(ctx,options,async token=> {
 var q=ctx.Request.Query;
 int Read(string key,int fallback)=>q.ContainsKey(key)?int.Parse(q[key]!,System.Globalization.CultureInfo.InvariantCulture):fallback;
 double? ReadCoordinate(string key)=>q.ContainsKey(key)?double.Parse(q[key]!,System.Globalization.CultureInfo.InvariantCulture):null;
 return (object)await sender.Send(new SearchCatalogueQuery(new(q["q"],q.ContainsKey("cityId")?Read("cityId",0):null,q["sources"].SelectMany(x=>x!.Split(',')).Where(x=>x.Length>0).ToArray(),q["sort"].FirstOrDefault(),Read("page",1),Read("pageSize",20),q["location"].FirstOrDefault(),q.ContainsKey("provinceId")?Read("provinceId",0):null,ReadCoordinate("latitude"),ReadCoordinate("longitude"))),token);
 },ct)).WithName("Catalogue.Items").WithTags("Catalogue");
 group.MapGet("/filters/locations",async(HttpContext ctx,ISender sender,IOptions<CatalogueOptions> options,CancellationToken ct)=>await Execute(ctx,options,async token=>(object)await sender.Send(new GetCatalogueLocationsQuery(),token),ct)).WithName("Catalogue.Locations").WithTags("Catalogue");
 group.MapGet("/filters/cities",async(HttpContext ctx,ISender sender,IOptions<CatalogueOptions> options,CancellationToken ct)=>await Execute(ctx,options,async token=>(object)await sender.Send(new GetCatalogueCitiesQuery(),token),ct)).WithName("Catalogue.Cities").WithTags("Catalogue");
 }
 private static async Task<IResult> Execute(HttpContext ctx,IOptions<CatalogueOptions> options,Func<CancellationToken,Task<object>> action,CancellationToken ct) {
 ctx.Response.Headers.CacheControl="no-store";
 if(!options.Value.Enabled)return Results.NotFound(ApiResponseHelper.Error("کاتالوگ فعال نیست.",statusCode:404));
 using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
 var watch=System.Diagnostics.Stopwatch.StartNew();var status=500;var partial=false;
 try{var value=await action(deadline.Token);status=200;partial=value is CatalogueResponse { IsPartial:true } or CatalogueLocationsResponse { IsPartial:true };return Results.Ok(ApiResponseHelper.Success(value));}
 catch(OperationCanceledException) when(!ct.IsCancellationRequested){status=503;return Results.Json(ApiResponseHelper.Error("مهلت پاسخ کاتالوگ تمام شد.",statusCode:503),statusCode:503);}
 catch(Exception ex) when(ex is ValidationException or FormatException or OverflowException){status=400;return Results.BadRequest(ApiResponseHelper.Error("پارامترهای جستجو معتبر نیستند.",statusCode:400));}
 catch(CatalogueUnavailableException){status=503;return Results.Json(ApiResponseHelper.Error("کاتالوگ در دسترس نیست.",statusCode:503),statusCode:503);}
 finally {if(ct.IsCancellationRequested)status=499;var tags=new System.Diagnostics.TagList{{"status",status},{"partial",partial}};Responses.Add(1,tags);Duration.Record(watch.Elapsed.TotalMilliseconds,tags);}
 }
}
