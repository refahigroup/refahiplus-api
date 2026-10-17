using Refahi.Modules.Pools.Domain;
using Refahi.Modules.Pools.Infrastructure.Providers.PoolTicket;
using Refahi.Modules.Pools.Application.Features.Purchases.Create;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Xunit;
namespace Refahi.Modules.Pools.Tests;
public sealed class PoolPurchaseTests
{
    private static readonly DateTimeOffset Now=new(2026,10,13,0,0,0,TimeSpan.Zero);
    private static PoolRequest Request(long total=100)=>PoolRequest.Create(Guid.NewGuid(),Guid.NewGuid(),"poolticket","12","9","{}","استخر","2026-10-14","10:00 - 12:00",1,1,total,Now.AddDays(1),Now.AddDays(1).AddHours(2));
    private static PoolPurchase Purchase(params PoolRequest[] requests)=>PoolPurchase.Create(Guid.NewGuid(),"key","hash",requests,Now,Now.AddMinutes(10));
    private static void Issue(PoolPurchase p){p.AttachOrder(Guid.NewGuid(),"R-1");p.CommitPaid(p.Requests.ToDictionary(x=>x.Id,x=>("hash"+x.Id,"cipher"+x.Id)),Now);}
    [Fact] public void Grouped_purchase_has_independent_requests_one_order_and_checked_amount()
    {var p=Purchase(Request(100),Request(200));Assert.Equal(300,p.TotalMinor);Assert.Equal(2,p.Requests.Select(x=>x.Id).Distinct().Count());var order=Guid.NewGuid();p.AttachOrder(order,"R-1");p.AttachOrder(order,"R-1");Assert.Throws<InvalidOperationException>(()=>p.AttachOrder(Guid.NewGuid(),"R-2"));Assert.Throws<OverflowException>(()=>Purchase(Request(long.MaxValue),Request()));}
    [Fact] public void Issuance_is_atomic_idempotent_and_requires_financial_attachment()
    {var p=Purchase(Request(),Request());var codes=p.Requests.ToDictionary(x=>x.Id,x=>("hash","cipher"));Assert.Throws<InvalidOperationException>(()=>p.CommitPaid(codes,Now));p.AttachOrder(Guid.NewGuid(),"R-1");codes[p.Requests.Last().Id]=("","");Assert.Throws<InvalidOperationException>(()=>p.CommitPaid(codes,Now));Assert.All(p.Requests,x=>Assert.Null(x.CodeHash));IssueAfterAttach(p);var hash=p.Requests.First().CodeHash;p.CommitPaid(new Dictionary<Guid,(string,string)>(),Now);Assert.Equal(hash,p.Requests.First().CodeHash);}
    private static void IssueAfterAttach(PoolPurchase p)=>p.CommitPaid(p.Requests.ToDictionary(x=>x.Id,x=>("hash"+x.Id,"cipher"+x.Id)),Now);
    [Fact] public void Consumption_uses_inclusive_start_exclusive_end_and_blocks_entire_refund()
    {var r=Request();var p=Purchase(r);Issue(p);Assert.False(r.IsRedeemable(r.StartsAtUtc.AddTicks(-1)));Assert.True(r.IsRedeemable(r.StartsAtUtc));Assert.False(r.IsRedeemable(r.EndsAtUtc));p.Redeem(r.Id,Guid.NewGuid(),"key","hash",r.StartsAtUtc);Assert.Throws<InvalidOperationException>(()=>p.BeginCancellation());Assert.Throws<InvalidOperationException>(()=>p.Redeem(r.Id,Guid.NewGuid(),"key2","hash",r.StartsAtUtc));}
    [Fact] public void Cancellation_revokes_before_refund_and_never_reopens_after_failure()
    {var p=Purchase(Request(),Request());Issue(p);p.BeginCancellation();Assert.Equal(PoolPurchaseStatus.CancellationPending,p.Status);Assert.All(p.Requests,x=>Assert.Equal(PoolVoucherStatus.Revoked,x.VoucherStatus));Assert.Throws<InvalidOperationException>(()=>p.CommitPaid(new Dictionary<Guid,(string,string)>(),Now));p.BeginCancellation();p.CompleteCancellation(true);p.CompleteCancellation(true);Assert.Equal(PoolPurchaseStatus.Refunded,p.Status);}
    [Fact] public void Expiry_preserves_codes_and_disables_consumption()
    {var r=Request();var p=Purchase(r);Issue(p);var cipher=r.CodeCiphertext;p.ExpireVouchers(r.EndsAtUtc);Assert.Equal(PoolVoucherStatus.Expired,r.VoucherStatus);Assert.Equal(cipher,r.CodeCiphertext);Assert.False(r.IsRedeemable(r.EndsAtUtc));p.BeginCancellation();Assert.Equal(PoolVoucherStatus.Revoked,r.VoucherStatus);}
    [Theory][InlineData("2026-10-13","10:00 - 12:00")][InlineData("۱۴۰۵/۰۷/۲۱","۱۰:۰۰ تا ۱۲:۰۰")]
    public void Valid_dates_parse_to_exact_tehran_utc(string date,string time)
    {var r=PoolSessionTime.Parse(date,time);Assert.Equal(new DateTimeOffset(2026,10,13,6,30,0,TimeSpan.Zero),r.Start);Assert.Equal(TimeSpan.FromHours(2),r.End-r.Start);}
    [Theory][InlineData("2026-10-13","10:00")][InlineData("2026-10-13","12:00 - 10:00")][InlineData("bad","10:00 - 12:00")][InlineData("1405/13/32","10:00 - 12:00")]
    public void Unknown_or_invalid_session_range_fails_closed(string date,string time)=>Assert.Null(PoolSessionTime.Parse(date,time).Start);
    [Fact] public void Input_validation_rejects_null_duplicate_selection_and_overflowing_counts()
    {var validator=new CreatePoolPurchaseValidator();var choice=new PoolChoice(Guid.NewGuid(),1,0);Assert.False(validator.Validate(new CreatePoolPurchaseCommand(Guid.NewGuid(),[choice,choice],100,"key")).IsValid);Assert.False(validator.Validate(new CreatePoolPurchaseCommand(Guid.NewGuid(),null!,100,"key")).IsValid);Assert.False(validator.Validate(new CreatePoolPurchaseCommand(Guid.NewGuid(),[choice with{AdultCount=int.MaxValue}],100,"key")).IsValid);}
}
