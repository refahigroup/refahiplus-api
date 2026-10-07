using System.Security.Claims;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Shared.Presentation;
using Refahi.Modules.Cinema.Application.Contracts;
using Refahi.Modules.Cinema.Application.Features.Catalog;
using Refahi.Modules.Cinema.Application.Features.Orders;

namespace Refahi.Modules.Cinema.Api.Endpoints;
public sealed record ReserveCinemaRequest(string ScheduleId,string[] SeatIds,string IdempotencyKey);
public sealed record CheckoutCinemaRequest(long Version,long TotalMinor);
public sealed class CinemaEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if(app is not IEndpointRouteBuilder routes)return;
        Public(routes.MapGet("/cities",(IMediator m,CancellationToken ct)=>Run(()=>m.Send(new CinemaCitiesQuery(),ct))),"Cities");
        Public(routes.MapGet("/landing",(int? city,IMediator m,CancellationToken ct)=>Run(()=>m.Send(new CinemaLandingQuery(city),ct))),"Landing");
        Public(routes.MapGet("/shows",(string? kind,int? city,string? search,int? page,IMediator m,CancellationToken ct)=>Run(()=>m.Send(new CinemaShowsQuery(kind??"cinema",city,search,page??1),ct))),"Shows");
        Public(routes.MapGet("/shows/{showId}",(string showId,IMediator m,CancellationToken ct)=>Run(()=>m.Send(new CinemaShowQuery(showId),ct))),"Show");
        Public(routes.MapGet("/shows/{showId}/places",(string showId,int? city,string? search,IMediator m,CancellationToken ct)=>Run(()=>m.Send(new CinemaPlacesQuery(showId,city,search),ct))),"Places");
        Public(routes.MapGet("/shows/{showId}/places/{placeId}/sessions",(string showId,string placeId,string? date,IMediator m,CancellationToken ct)=>Run(()=>m.Send(new CinemaSessionsQuery(showId,placeId,date),ct))),"Sessions");
        Public(routes.MapGet("/schedules/{scheduleId}/seats",(string scheduleId,HttpContext h,IMediator m,CancellationToken ct)=>{NoStore(h);return Run(()=>m.Send(new CinemaSeatsQuery(scheduleId),ct));}),"Seats");
        Public(routes.MapGet("/schedules/{scheduleId}/seats/status",(string scheduleId,HttpContext h,IMediator m,CancellationToken ct)=>{NoStore(h);return Run(()=>m.Send(new CinemaStatusesQuery(scheduleId),ct));}),"SeatStatuses");
        Private(routes.MapPost("/cinema-orders",(ReserveCinemaRequest r,HttpContext h,IMediator m,CancellationToken ct)=>Run(()=>m.Send(new ReserveCinemaOrderCommand(User(h),r.ScheduleId,r.SeatIds,(r.IdempotencyKey??"").Trim()),ct))),"Reserve");
        Private(routes.MapPost("/cinema-orders/{id:guid}/checkout",(Guid id,CheckoutCinemaRequest r,HttpContext h,IMediator m,CancellationToken ct)=>Run(()=>m.Send(new CheckoutCinemaOrderCommand(id,User(h),r.Version,r.TotalMinor),ct))),"Checkout");
        Private(routes.MapGet("/cinema-orders/{id:guid}",(Guid id,HttpContext h,IMediator m,CancellationToken ct)=>{NoStore(h);return Run(()=>m.Send(new GetCinemaOrderQuery(id,User(h)),ct));}),"Order");
        Private(routes.MapPost("/cinema-orders/{id:guid}/cancel",(Guid id,HttpContext h,IMediator m,CancellationToken ct)=>Run(()=>m.Send(new CancelCinemaOrderCommand(id,User(h)),ct))),"Cancel");
        Private(routes.MapGet("/cinema-orders/{id:guid}/ticket",(Guid id,HttpContext h,IMediator m,CancellationToken ct)=>{NoStore(h);return Run(async()=>{var r=await m.Send(new GetCinemaOrderQuery(id,User(h)),ct);if(!r.CanDownload)throw new CinemaException("بلیط آماده دریافت نیست");return r;});}),"Ticket");
        Private(routes.MapGet("/cinema-orders/{id:guid}/ticket/download",async(Guid id,HttpContext h,IMediator m,ICinemaTicketRenderer renderer,CancellationToken ct)=>
        {
            NoStore(h);
            try {var ticket=await m.Send(new GetCinemaOrderQuery(id,User(h)),ct);return Results.File(await renderer.RenderAsync(ticket,ct),"application/pdf",$"refahi-ticket-{id:N}.pdf");}
            catch(CinemaException ex){return Error(ex.Message,ex.Status);}
            catch(Exception ex)when(ex is not OperationCanceledException){return Error("دریافت فایل بلیط در حال حاضر ممکن نیست",503);}
        }),"Download");
    }
    private static void Public(RouteHandlerBuilder b,string name)=>b.WithName("Cinema."+name).WithTags("Cinema").AllowAnonymous().Produces(200).Produces(400).Produces(503);
    private static void Private(RouteHandlerBuilder b,string name)=>b.WithName("Cinema."+name).WithTags("Cinema").RequireAuthorization("UserOrAdmin").Produces(200).Produces(401).Produces(403).Produces(409);
    private static void NoStore(HttpContext h)=>h.Response.Headers.CacheControl="private, no-store";
    private static Guid User(HttpContext h)=>Guid.TryParse(h.User.FindFirstValue(ClaimTypes.NameIdentifier)??h.User.FindFirstValue("sub"),out var id)?id:throw new CinemaException("ابتدا وارد حساب کاربری شوید",401);
    private static IResult Error(string message,int status)=>Results.Json(ApiResponseHelper.Error(message,statusCode:status),statusCode:status);
    private static async Task<IResult> Run<T>(Func<Task<T>> work)
    {
        try{return Results.Ok(ApiResponseHelper.Success(await work()));}
        catch(CinemaException ex){return Error(ex.Message,ex.Status);}
        catch(ValidationException ex){return Results.Json(ApiResponseHelper.ValidationError(ex.Errors.GroupBy(x=>x.PropertyName).ToDictionary(x=>x.Key,x=>x.Select(e=>e.ErrorMessage).ToArray())),statusCode:400);}
        catch(UnauthorizedAccessException){return Error("دسترسی به سفارش مجاز نیست",403);}
        catch(Exception ex)when(ex is not OperationCanceledException){return Error("سرویس سینما در حال حاضر پاسخگو نیست؛ دوباره تلاش کنید",503);}
    }
}
