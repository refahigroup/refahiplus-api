using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Shared.Presentation;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
using Refahi.Modules.Media.Application.Contracts.External;
namespace Refahi.Modules.Hotels.Api.EndPoints.Catalogue;
public sealed class HotelDiscoveryProfileEndpoint : IEndpoint
{
 public void Map(object app) {
 if(app is not IEndpointRouteBuilder routes)return;
 routes.MapGet("/v2/{hotelKey:guid}/profile",async(Guid hotelKey,HttpContext ctx,IHotelDiscoveryReader reader,IExternalMediaService media,CancellationToken ct)=> {
 ctx.Response.Headers.CacheControl="no-store";
 try {
 var profile=await reader.ProfileAsync(hotelKey,ct);if(profile is null)return Results.NotFound(ApiResponseHelper.Error("هتل یافت نشد.",statusCode:404));
 IReadOnlyList<string> images=profile.IsLocalContent?profile.Images:[];try{if(!profile.IsLocalContent)images=await media.GetPublicUrlsAsync(profile.Images,new("Hotels",profile.Provider),ct);}catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}catch{}
 return Results.Ok(ApiResponseHelper.Success(profile with {Images=images}));
 }catch(Refahi.Modules.Catalogue.Application.Contracts.CatalogueUnavailableException){return Results.Json(ApiResponseHelper.Error("اطلاعات هتل در حال به‌روزرسانی است.",statusCode:503),statusCode:503);}
 }).WithName("Hotels.Discovery.Profile").WithTags("Hotels");
 }
}
