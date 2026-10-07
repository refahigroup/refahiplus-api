using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Cinema.Application.Contracts;
using Refahi.Modules.Cinema.Domain;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.Queries;

namespace Refahi.Modules.Cinema.Application.Features.Orders;
public sealed class CinemaOrderService(ICinemaOrderRepository repository, ICinemaProviderFactory factory,
    ICinemaMutationLock gate, IMediator mediator, IOptions<CinemaOptions> options, ILogger<CinemaOrderService> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static CinemaSeatMap Snapshot(CinemaOrder order)=>JsonSerializer.Deserialize<CinemaSeatMap>(order.SnapshotJson,Json)!;
    public static string[] SeatIds(CinemaOrder order)=>JsonSerializer.Deserialize<string[]>(order.SeatIdsJson,Json)!;
    private ICinemaProvider Provider(CinemaOrder order)=>factory.Get(order.ProviderKey);
    private void ApplyIssuance(CinemaOrder order,CinemaReservation reservation)
    {
        if(string.IsNullOrWhiteSpace(reservation.Code)&&string.IsNullOrWhiteSpace(reservation.SamfaCode))
        {
            if(order.IssuanceStatus!="NeedsReview")logger.LogWarning("Cinema issuance missing entrance code. CinemaOrderId={CinemaOrderId} OrderId={OrderId}",order.Id,order.OrderId);
            order.RequireReview("تأیید تأمین‌کننده دریافت شد؛ کد معتبر بلیط هنوز در دسترس نیست");
        }
        else order.Issue(reservation.Code,reservation.SamfaCode);
    }
    public async Task<CinemaOrder> Owned(Guid id,Guid user,CancellationToken ct)
    {
        var order=await repository.GetAsync(id,ct)??throw new CinemaException("سفارش سینما یافت نشد",404);
        if(order.UserId!=user)throw new CinemaException("دسترسی به سفارش مجاز نیست",403);
        return order;
    }
    public static CinemaOrderView View(CinemaOrder order, bool cancellationEnabled=true)=>new(order.Id,order.OrderId,order.OrderNumber,order.Version,
        order.PaymentStatus,order.IssuanceStatus,order.CancellationStatus,order.PayableUntil,order.TotalMinor,order.SubtotalMinor,
        order.DiscountMinor,order.FeeMinor,order.TaxMinor,Snapshot(order),SeatIds(order),order.TicketCode,order.SamfaCode,
        order.IsPayable(DateTimeOffset.UtcNow)&&order.OrderId==null,order.HasTicket,
        order.CancellationStatus=="None"&&order.IssuanceStatus is "Reserved" or "Issued" && (order.PaymentStatus!="Paid"||cancellationEnabled),order.Message);
    public async Task<CinemaOrderView> GetAsync(Guid id,Guid user,CancellationToken ct)
    {
        var order=await Owned(id,user,ct);
        if(order.OrderId.HasValue)
        {
            var financial=await mediator.Send(new GetOrderByIdQuery(order.OrderId.Value,user,"User"),ct);
            if(financial?.PaymentState=="Paid"&&order.PaymentStatus=="Unpaid")
            { await using var lease=await gate.AcquireAsync(id,ct);order.MarkPaid();await repository.SaveAsync(ct); }
            if(financial?.PaymentState=="Refunded"&&order.PaymentStatus!="Refunded")
            { await using var lease=await gate.AcquireAsync(id,ct);order.MarkRefunded();await repository.SaveAsync(ct); }
        }
        return View(order,options.Value.CancellationEnabled);
    }
    public async Task<CinemaOrderView> ReserveAsync(Guid user,string schedule,string[] seats,string key,CancellationToken ct)
    {
        if(!options.Value.PurchaseEnabled)throw new CinemaException("خرید بلیط هنوز فعال نشده است",503);
        var lockId=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{user:N}:{key}"))[..16]);
        await using var lease=await gate.AcquireAsync(lockId,ct);
        var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(schedule+":"+string.Join(",",seats.Order()))));
        var old=await repository.FindAsync(user,key,ct);
        if(old!=null)
        {if(old.Fingerprint!=fingerprint)throw new CinemaException("کلید درخواست قبلاً برای سفارش دیگری استفاده شده است");return View(old,options.Value.CancellationEnabled);}
        var customer=await mediator.Send(new GetUserCommerceContactQuery(user),ct);
        if(customer?.MobileNumber is not string mobile||!System.Text.RegularExpressions.Regex.IsMatch(mobile,"^09[0-9]{9}$"))
            throw new CinemaException("شماره همراه معتبر در حساب کاربری ثبت نشده است",400);
        var provider=factory.Get(options.Value.ProviderKey);var map=await provider.SeatsAsync(schedule,ct);
        var live=await provider.StatusAsync(schedule,ct);
        if(map.StartsAt<=DateTimeOffset.UtcNow||map.PurchaseEndAt<=DateTimeOffset.UtcNow)
            throw new CinemaException("فروش این سانس پایان یافته است");
        if(seats.Any(id=>!map.Seats.Any(s=>s.Id==id&&s.IsBookable)||!live.Seats.TryGetValue(id,out var status)||status!="available"))
            throw new CinemaException("یک یا چند صندلی دیگر قابل رزرو نیست؛ نقشه را تازه کنید");
        var order=CinemaOrder.Create(user,provider.Key,schedule,key,fingerprint,JsonSerializer.Serialize(map,Json),JsonSerializer.Serialize(seats,Json),map.Show.Kind=="theater"?"theater":"cinema");
        var attempt=order.BeginAttempt("reserve");await repository.AddAsync(order,ct);
        try
        {
            var result=await provider.ReserveAsync(schedule,seats,new(mobile,customer.FullName),ct);
            order.RecordProviderReference(result.Id);
            var deadline=result.ExpiresAt;
            if(map.PurchaseEndAt.HasValue&&(!deadline.HasValue||map.PurchaseEndAt<deadline))deadline=map.PurchaseEndAt;
            // Missing provider expiry never creates a payable order.
            if(!result.ExpiresAt.HasValue)deadline=null;
            deadline=deadline?.AddSeconds(-Math.Max(60,options.Value.SafetySeconds));
            order.Reserve(result.Id,result.Code,deadline,result.SubtotalMinor,result.DiscountMinor,result.FeeMinor,result.TaxMinor,result.TotalMinor);
            if(!provider.IsReserved(result.Status))order.RequireReview("وضعیت رزرو نیازمند بررسی است");
            attempt.Complete();await repository.SaveAsync(CancellationToken.None);
        }
        catch(CinemaProviderAmbiguousException ex)
        {if(ex.ProviderOrderId!=null)order.RecordProviderReference(ex.ProviderOrderId);attempt.Fail(true);order.RequireReview("نتیجه رزرو مشخص نیست؛ از ثبت دوباره خرید خودداری کنید");await repository.SaveAsync(CancellationToken.None);}
        catch(Exception ex)
        {
            attempt.Fail(ex is not CinemaException);if(ex is CinemaException)order.MarkFailed(ex.Message);
            else order.RequireReview("پاسخ رزرو نیازمند بررسی است");await repository.SaveAsync(CancellationToken.None);
            logger.LogWarning("Cinema reservation requires attention. CinemaOrderId={CinemaOrderId} Outcome={Outcome}",order.Id,attempt.Outcome);
        }
        return View(order,options.Value.CancellationEnabled);
    }
    public async Task<CinemaCheckoutResult> CheckoutAsync(Guid id,Guid user,long version,long amount,CancellationToken ct)
    {
        if(!options.Value.PurchaseEnabled)throw new CinemaException("خرید بلیط غیرفعال است",503);
        await using var lease=await gate.AcquireAsync(id,ct);var order=await Owned(id,user,ct);
        using var correlation=logger.BeginScope(new Dictionary<string,object?>{{"CinemaOrderId",id},{"OrderId",order.OrderId},{"ProviderOrderId",order.ProviderOrderId},{"Operation","checkout"}});
        if(order.OrderId.HasValue)return new(order.Id,order.OrderId.Value,order.OrderNumber!,order.TotalMinor);
        if(!order.IsPayable(DateTimeOffset.UtcNow)||order.Version!=version||order.TotalMinor!=amount)
            throw new CinemaException("رزرو یا مبلغ تغییر کرده است؛ سفارش را دوباره بررسی کنید");
        var snapshot=Snapshot(order);var selected=snapshot.Seats.Where(s=>SeatIds(order).Contains(s.Id)).ToArray();
        // A single financial line owns the exact provider total, including fees, taxes and discounts.
        var response=await mediator.Send(new CreateOrderCommand(user,"Cinema",id,
            [new(snapshot.Show.Title+" — "+snapshot.Place.Title+" — "+string.Join("، ",selected.Select(s=>$"ردیف {s.Row} صندلی {s.Number}")),
                order.TotalMinor,1,0,id,order.CategoryCode,null,order.SeatIdsJson)],$"cinema-order:{id:N}",
            ReferenceType:"CinemaOrder",SagaId:id,PayableUntil:order.PayableUntil),ct);
        order.AttachOrder(response.OrderId,response.OrderNumber);await repository.SaveAsync(ct);
        return new(id,response.OrderId,response.OrderNumber,response.FinalAmountMinor);
    }
    public async Task<CinemaOrderView> CancelAsync(Guid id,Guid user,CancellationToken ct)
    {
        var order=await Owned(id,user,ct);
        if(order.OrderId is Guid financial)
        {
            await mediator.Send(new CancelOrderCommand(financial,"لغو بلیط سینما",$"cinema-cancel:{id:N}",CallerUserId:user,CallerRole:"User"),ct);
            return await GetAsync(id,user,ct);
        }
        await using var lease=await gate.AcquireAsync(id,ct);
        await CancelProviderAsync(order,ct);
        return View(order,options.Value.CancellationEnabled);
    }
    public async Task<bool> CancelProviderAsync(CinemaOrder order,CancellationToken ct)
    {
        if(order.CancellationStatus=="Cancelled")return true;
        if(order.ProviderOrderId==null)throw new CinemaException("نتیجه رزرو ابتدا باید مشخص شود");
        if(order.PaymentStatus=="Paid"&&!options.Value.CancellationEnabled&&order.IssuanceStatus!="Failed"&&order.CancellationStatus!="Pending")
            throw new CinemaException("لغو بلیط پرداخت‌شده هنوز فعال نشده است");
        var provider=Provider(order);
        using var correlation=logger.BeginScope(new Dictionary<string,object?>{{"CinemaOrderId",order.Id},{"OrderId",order.OrderId},{"ProviderOrderId",order.ProviderOrderId},{"Operation","cancel"}});
        if(order.Attempts.Any(a=>a.Operation=="confirm"&&a.Outcome is "Started" or "Ambiguous")||order.CancellationStatus=="Pending")
        {
            var known=await provider.GetReservationAsync(order.ProviderOrderId,ct);
            if(provider.IsCancelled(known.Status)){order.MarkCancelled();await repository.SaveAsync(ct);return true;}
            if(order.CancellationStatus=="Pending")return false;
            if(!provider.IsConfirmed(known.Status)&&!provider.IsReserved(known.Status))return false;
        }
        if(order.Attempts.Any(a=>a.Operation=="cancel"&&a.Outcome=="Failed"))return false;
        order.BeginCancellation();var attempt=order.BeginAttempt("cancel");await repository.SaveAsync(ct);
        try {await provider.CancelAsync(order.ProviderOrderId,ct);attempt.Complete();order.MarkCancelled();await repository.SaveAsync(CancellationToken.None);return true;}
        catch(CinemaProviderAmbiguousException){attempt.Fail(true);await repository.SaveAsync(CancellationToken.None);logger.LogWarning("Cinema cancellation outcome ambiguous. CinemaOrderId={CinemaOrderId}",order.Id);return false;}
        catch(CinemaException){attempt.Fail(false);order.RejectCancellation();await repository.SaveAsync(CancellationToken.None);return false;}
    }
    public async Task FinalizeAsync(Guid id,CancellationToken ct)
    {
        await using var lease=await gate.AcquireAsync(id,ct);
        var order=await repository.GetAsync(id,ct);if(order?.OrderId is not Guid financial||order.HasTicket||order.CancellationStatus!="None")return;
        using var correlation=logger.BeginScope(new Dictionary<string,object?>{{"CinemaOrderId",id},{"OrderId",financial},{"ProviderOrderId",order.ProviderOrderId},{"Operation","confirm"}});
        var paid=await mediator.Send(new GetOrderByIdQuery(financial,order.UserId,"User"),ct);
        if(paid?.PaymentState!="Paid"||paid.SourceReferenceId!=id||paid.SourceModule!="Cinema"||paid.FinalAmountMinor!=order.TotalMinor)return;
        order.MarkPaid();await repository.SaveAsync(ct);
        if(order.ProviderOrderId==null){order.RequireReview("شناسه رزرو برای صدور موجود نیست");await repository.SaveAsync(ct);return;}
        var provider=Provider(order);var known=await provider.GetReservationAsync(order.ProviderOrderId,ct);
        if(provider.IsConfirmed(known.Status)){ApplyIssuance(order,known);await repository.SaveAsync(ct);return;}
        if(order.Attempts.Any(a=>a.Operation=="confirm"&&a.Outcome is "Started" or "Ambiguous"))return;
        if(!provider.IsReserved(known.Status)){order.MarkFailed("رزرو تأمین‌کننده قابل صدور نیست");await repository.SaveAsync(ct);return;}
        var attempt=order.BeginAttempt("confirm");await repository.SaveAsync(ct);
        try
        {
            var result=await provider.ConfirmAsync(order.ProviderOrderId,ct);
            if(!provider.IsConfirmed(result.Status))throw new CinemaProviderAmbiguousException(new InvalidOperationException());
            attempt.Complete();ApplyIssuance(order,result);await repository.SaveAsync(CancellationToken.None);
        }
        catch(CinemaProviderAmbiguousException){attempt.Fail(true);order.RequireReview("پرداخت انجام شد؛ نتیجه صدور در حال بررسی است");await repository.SaveAsync(CancellationToken.None);logger.LogWarning("Cinema paid issuance outcome ambiguous. CinemaOrderId={CinemaOrderId}",id);}
        catch(CinemaException ex){attempt.Fail(false);order.MarkFailed(ex.Message);await repository.SaveAsync(CancellationToken.None);logger.LogWarning("Cinema paid issuance failed; compensation pending. CinemaOrderId={CinemaOrderId}",id);}
    }
    public async Task ReconcileAsync(Guid id,CancellationToken ct)
    {
        var order=await repository.GetAsync(id,ct);if(order==null)return;
        if(order.IssuanceStatus=="Reserving"&&order.ProviderOrderId==null&&order.UpdatedAt<DateTimeOffset.UtcNow.AddMinutes(-2))
        {await using var lease=await gate.AcquireAsync(id,ct);order=await repository.GetAsync(id,ct);if(order?.IssuanceStatus=="Reserving"){order.RequireReview("نتیجه رزرو پس از قطع ارتباط نیازمند بررسی است");await repository.SaveAsync(ct);}return;}
        if(order.OrderId==null&&order.ProviderOrderId!=null)
        {
            var existing=await mediator.Send(new GetOrderByIdempotencyKeyQuery($"cinema-order:{id:N}",order.UserId,"Cinema"),ct);
            if(existing!=null){await using var lease=await gate.AcquireAsync(id,ct);order.AttachOrder(existing.Id,existing.OrderNumber);await repository.SaveAsync(ct);}
        }
        if(order.OrderId is Guid financial)
        {
            var payment=await mediator.Send(new GetOrderByIdQuery(financial,order.UserId,"User"),ct);
            if(payment?.PaymentState=="Paid"&&order.CancellationStatus=="None"&&order.IssuanceStatus!="Failed")await FinalizeAsync(id,ct);
            if(payment?.PaymentState=="Refunded"){await using var lease=await gate.AcquireAsync(id,ct);order.MarkRefunded();await repository.SaveAsync(ct);return;}
            if(order.CancellationStatus=="Pending"||order.CancellationStatus=="Cancelled"||order.IssuanceStatus=="Failed"
                ||payment?.PaymentState!="Paid"&&order.PayableUntil<=DateTimeOffset.UtcNow)
                await mediator.Send(new CancelOrderCommand(financial,"بازیابی یا انقضای سفارش سینما",$"cinema-cancel:{id:N}",CallerUserId:order.UserId,CallerRole:"System"),ct);
        }
        else if(order.ProviderOrderId!=null&&(order.PayableUntil==null||order.PayableUntil<=DateTimeOffset.UtcNow||order.CancellationStatus=="Pending"))
        {await using var lease=await gate.AcquireAsync(id,ct);await CancelProviderAsync(order,ct);}
    }
}
