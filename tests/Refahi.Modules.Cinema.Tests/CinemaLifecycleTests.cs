using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Refahi.Modules.Cinema.Domain;
using Refahi.Modules.Cinema.Application.Contracts;
using Refahi.Modules.Cinema.Application.Features.Orders;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Orders.Application.Contracts.Dtos;
using Xunit;
namespace Refahi.Modules.Cinema.Tests;
public sealed class CinemaLifecycleTests
{
    private const string Schedule="01m4442930npemsr3a8c7nxfyt",Seat="01ktm9ahkjsyr3cc8b3n1046r1";
    [Fact]public async Task Concurrent_reservations_share_one_provider_call()
    {
        var f=new Fixture();var results=await Task.WhenAll(f.Reserve(),f.Reserve());Assert.Equal(results[0].Id,results[1].Id);Assert.Equal(1,f.Provider.ReserveCalls);
    }
    [Fact]public async Task Key_reuse_with_different_seats_is_rejected()
    {
        var f=new Fixture();await f.Reserve();await Assert.ThrowsAsync<CinemaException>(()=>f.Service.ReserveAsync(f.User,Schedule,["01ktm9ahkpd6ax7k6t0c6z6f92"],"key",default));
    }
    [Fact]public async Task Lost_reserve_response_is_persisted_and_never_automatically_retried()
    {
        var f=new Fixture();f.Provider.AmbiguousReserve=true;var result=await f.Reserve();var replay=await f.Reserve();
        Assert.Equal("NeedsReview",result.IssuanceStatus);Assert.False(replay.CanCheckout);Assert.Equal(1,f.Provider.ReserveCalls);Assert.Equal("Ambiguous",Assert.Single(f.Repository.Order!.Attempts).Outcome);
    }
    [Fact]public async Task Missing_expiry_does_not_allow_checkout()
    {
        var f=new Fixture();f.Provider.Expiry=null;var result=await f.Reserve();Assert.False(result.CanCheckout);
        await Assert.ThrowsAsync<CinemaException>(()=>f.Service.CheckoutAsync(result.Id,f.User,result.Version,result.TotalMinor,default));
    }
    [Fact]public async Task Price_change_or_stale_version_requires_new_review()
    {
        var f=new Fixture();var r=await f.Reserve();await Assert.ThrowsAsync<CinemaException>(()=>f.Service.CheckoutAsync(r.Id,f.User,r.Version,r.TotalMinor+1,default));
        await Assert.ThrowsAsync<CinemaException>(()=>f.Service.CheckoutAsync(r.Id,f.User,r.Version-1,r.TotalMinor,default));
        Assert.Equal(0,f.Mediator.CreateCalls);
    }
    [Fact]public async Task Checkout_uses_provider_total_and_returns_same_financial_order()
    {
        var f=new Fixture();var r=await f.Reserve();var a=await f.Service.CheckoutAsync(r.Id,f.User,r.Version,r.TotalMinor,default);var b=await f.Service.CheckoutAsync(r.Id,f.User,r.Version,r.TotalMinor,default);
        Assert.Equal(a.OrderId,b.OrderId);Assert.Equal(1,f.Mediator.CreateCalls);Assert.Equal("Cinema",f.Mediator.Create!.SourceModule);Assert.Equal(r.TotalMinor,f.Mediator.Create.Items.Single().UnitPriceMinor);
        Assert.Equal("cinema",f.Mediator.Create.Items.Single().CategoryCode);Assert.Equal(r.PayableUntil,f.Mediator.Create.PayableUntil);
    }
    [Fact]public async Task Other_user_cannot_read_checkout_or_cancel()
    {
        var f=new Fixture();var r=await f.Reserve();await Assert.ThrowsAsync<CinemaException>(()=>f.Service.GetAsync(r.Id,Guid.NewGuid(),default));
        await Assert.ThrowsAsync<CinemaException>(()=>f.Service.CheckoutAsync(r.Id,Guid.NewGuid(),r.Version,r.TotalMinor,default));
        await Assert.ThrowsAsync<CinemaException>(()=>f.Service.CancelAsync(r.Id,Guid.NewGuid(),default));
    }
    [Fact]public async Task Payment_gate_rejects_missing_authoritative_expiry_even_when_local_deadline_is_valid()
    {
        var f=new Fixture();var r=await f.Reserve();var checkout=await f.Service.CheckoutAsync(r.Id,f.User,r.Version,r.TotalMinor,default);
        f.Provider.Expiry=null;
        var participant=new CinemaPaymentParticipant(new Gate(),f.Service,f.Provider);
        await Assert.ThrowsAsync<CinemaException>(()=>participant.AcquireAsync(new(checkout.OrderId,f.User,r.Id,r.TotalMinor),default));
    }
    [Fact]public async Task Paid_event_replay_issues_once_and_never_issues_unpaid_order()
    {
        var f=new Fixture();var r=await f.Reserve();await f.Service.CheckoutAsync(r.Id,f.User,r.Version,r.TotalMinor,default);
        await f.Service.FinalizeAsync(r.Id,default);Assert.Equal(0,f.Provider.ConfirmCalls);
        f.Mediator.Paid=true;await f.Service.FinalizeAsync(r.Id,default);await f.Service.FinalizeAsync(r.Id,default);
        Assert.Equal(1,f.Provider.ConfirmCalls);Assert.True(f.Repository.Order!.HasTicket);
    }
    [Fact]public async Task Lost_confirm_response_is_recovered_by_lookup_without_second_confirmation()
    {
        var f=new Fixture();var r=await f.Reserve();await f.Service.CheckoutAsync(r.Id,f.User,r.Version,r.TotalMinor,default);f.Mediator.Paid=true;f.Provider.AmbiguousConfirm=true;
        await f.Service.FinalizeAsync(r.Id,default);Assert.Equal("NeedsReview",f.Repository.Order!.IssuanceStatus);
        await f.Service.FinalizeAsync(r.Id,default);Assert.Equal(1,f.Provider.ConfirmCalls);Assert.True(f.Repository.Order.HasTicket);
    }
    [Fact]public async Task Confirmed_order_without_an_entrance_code_stays_under_review()
    {
        var f=new Fixture();var r=await f.Reserve();await f.Service.CheckoutAsync(r.Id,f.User,r.Version,r.TotalMinor,default);
        f.Mediator.Paid=true;f.Provider.MissingTicketCode=true;
        await f.Service.FinalizeAsync(r.Id,default);await f.Service.FinalizeAsync(r.Id,default);
        Assert.Equal("NeedsReview",f.Repository.Order!.IssuanceStatus);Assert.False(f.Repository.Order.HasTicket);Assert.Equal(1,f.Provider.ConfirmCalls);
        f.Provider.MissingTicketCode=false;await f.Service.FinalizeAsync(r.Id,default);Assert.True(f.Repository.Order.HasTicket);
    }
    [Fact]public async Task Ambiguous_cancel_blocks_refund_until_provider_lookup_confirms_cancellation()
    {
        var f=new Fixture();await f.Reserve();f.Provider.AmbiguousCancel=true;
        Assert.False(await f.Service.CancelProviderAsync(f.Repository.Order!,default));Assert.Equal("Pending",f.Repository.Order!.CancellationStatus);
        Assert.True(await f.Service.CancelProviderAsync(f.Repository.Order,default));Assert.Equal(1,f.Provider.CancelCalls);Assert.False(f.Repository.Order.HasTicket);
    }
    [Fact]public void Domain_rejects_inconsistent_money_and_cancelled_ticket()
    {
        var order=CinemaOrder.Create(Guid.NewGuid(),"iticket",Schedule,"key","hash","{}","[]","cinema");
        Assert.Throws<InvalidOperationException>(()=>order.Reserve("id","code",DateTimeOffset.UtcNow.AddMinutes(5),100,0,0,0,99));
        order.Reserve("id","code",DateTimeOffset.UtcNow.AddMinutes(5),100,0,0,0,100);order.MarkPaid();order.BeginCancellation();
        Assert.Throws<InvalidOperationException>(()=>order.Issue("code",null));
    }
    private sealed class Fixture
    {
        public Guid User=Guid.NewGuid();public Repo Repository=new();public Provider Provider=new();public Mediator Mediator;
        public CinemaOrderService Service;
        public Fixture(){Mediator=new(Repository);Service=new(Repository,Provider,new Gate(),Mediator,Options.Create(new CinemaOptions{PurchaseEnabled=true,CancellationEnabled=true}),NullLogger<CinemaOrderService>.Instance);}
        public Task<CinemaOrderView> Reserve()=>Service.ReserveAsync(User,Schedule,[Seat],"key",default);
    }
    private sealed class Repo : ICinemaOrderRepository
    {
        public CinemaOrder? Order;
        public Task<CinemaOrder?> GetAsync(Guid id,CancellationToken ct)=>Task.FromResult(Order?.Id==id?Order:null);
        public Task<CinemaOrder?> FindAsync(Guid user,string key,CancellationToken ct)=>Task.FromResult(Order?.UserId==user&&Order.IdempotencyKey==key?Order:null);
        public Task<IReadOnlyList<Guid>> CandidatesAsync(CancellationToken ct,int skip=0)=>Task.FromResult<IReadOnlyList<Guid>>(Order==null||skip>0?[]:[Order.Id]);
        public Task AddAsync(CinemaOrder order,CancellationToken ct){Order=order;return Task.CompletedTask;}
        public Task SaveAsync(CancellationToken ct)=>Task.CompletedTask;
    }
    private sealed class Gate : ICinemaMutationLock
    {
        private readonly SemaphoreSlim semaphore=new(1,1);
        public async Task<IAsyncDisposable> AcquireAsync(Guid id,CancellationToken ct){await semaphore.WaitAsync(ct);return new Lease(semaphore);}
        private sealed class Lease(SemaphoreSlim semaphore) : IAsyncDisposable {public ValueTask DisposeAsync(){semaphore.Release();return ValueTask.CompletedTask;}}
    }
    private sealed class Provider : ICinemaProvider,ICinemaProviderFactory
    {
        public string Key=>"iticket";public ICinemaProvider Get(string key)=>this;
        public int ReserveCalls,ConfirmCalls,CancelCalls;public bool AmbiguousReserve,AmbiguousConfirm,AmbiguousCancel,MissingTicketCode;
        public DateTimeOffset? Expiry=DateTimeOffset.UtcNow.AddMinutes(10);private string status="reserved";
        private CinemaReservation Reservation()=>new("provider",MissingTicketCode?"":"ticket",status,Expiry,100000,0,10000,5000,115000,MissingTicketCode?null:"samfa",1);
        public bool IsReserved(string s)=>s=="reserved";public bool IsConfirmed(string s)=>s=="confirmed";public bool IsCancelled(string s)=>s=="cancelled";
        public Task<CinemaSeatMap> SeatsAsync(string schedule,CancellationToken ct)=>Task.FromResult(new CinemaSeatMap(schedule,new("show","فیلم","cinema",null,null,null,null,90,null,[]),new("place","سینما",null,null),"سالن",DateTimeOffset.UtcNow.AddDays(1),DateTimeOffset.UtcNow.AddDays(1),[new(Seat,"block","1","1",0,0,100000,"available",true,null)]));
        public Task<CinemaSeatStatuses> StatusAsync(string schedule,CancellationToken ct)=>Task.FromResult(new CinemaSeatStatuses(new Dictionary<string,string>{{Seat,"available"}}));
        public Task<CinemaReservation> ReserveAsync(string schedule,IReadOnlyList<string> seats,CinemaCustomer customer,CancellationToken ct){ReserveCalls++;if(AmbiguousReserve)throw new CinemaProviderAmbiguousException(new TimeoutException());return Task.FromResult(Reservation());}
        public Task<CinemaReservation> GetReservationAsync(string id,CancellationToken ct)=>Task.FromResult(Reservation());
        public Task<CinemaReservation> ConfirmAsync(string id,CancellationToken ct){ConfirmCalls++;status="confirmed";if(AmbiguousConfirm)throw new CinemaProviderAmbiguousException(new TimeoutException());return Task.FromResult(Reservation());}
        public Task CancelAsync(string id,CancellationToken ct){CancelCalls++;status="cancelled";if(AmbiguousCancel)throw new CinemaProviderAmbiguousException(new TimeoutException());return Task.CompletedTask;}
        public Task<IReadOnlyList<CinemaCity>> CitiesAsync(CancellationToken ct)=>throw new NotSupportedException();
        public Task<CinemaLanding> LandingAsync(int? city,CancellationToken ct)=>throw new NotSupportedException();
        public Task<CinemaCatalog> ShowsAsync(string kind,int? city,string? search,int page,CancellationToken ct)=>throw new NotSupportedException();
        public Task<CinemaShow> ShowAsync(string id,CancellationToken ct)=>throw new NotSupportedException();
        public Task<IReadOnlyList<CinemaDisplayDay>> PlacesAsync(string show,int? city,string? search,CancellationToken ct)=>throw new NotSupportedException();
        public Task<IReadOnlyList<CinemaSession>> SessionsAsync(string show,string place,string? date,CancellationToken ct)=>throw new NotSupportedException();
    }
    private sealed class Mediator(Repo repository) : IMediator
    {
        public bool Paid;public int CreateCalls;public CreateOrderCommand? Create;private Guid id=Guid.NewGuid();
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request,CancellationToken ct=default)
        {
            object? result=request switch
            {
                GetUserCommerceContactQuery q=>new UserCommerceContactDto(q.UserId,"کاربر","09123456789"),
                CreateOrderCommand c=>CreateOrder(c),
                GetOrderByIdQuery q=>new OrderDto(id,"R-1",repository.Order!.UserId,115000,0,0,null,0,115000,"Confirmed",Paid?"Paid":"Pending","Cinema",repository.Order.Id,"CinemaOrder",null,null,null,0,[],DateTimeOffset.UtcNow),
                _=>null
            };
            return Task.FromResult((TResponse)result!);
        }
        private CreateOrderResponse CreateOrder(CreateOrderCommand c){CreateCalls++;Create=c;return new(id,"R-1",c.Items.Sum(x=>x.UnitPriceMinor));}
        public Task<object?> Send(object request,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task Publish(object notification,CancellationToken ct=default)=>Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification,CancellationToken ct=default)where TNotification:INotification=>Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request,CancellationToken ct=default)=>throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request,CancellationToken ct=default)=>throw new NotSupportedException();
    }
}
