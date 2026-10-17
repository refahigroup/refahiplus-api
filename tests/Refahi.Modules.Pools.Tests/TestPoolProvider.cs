using MediatR;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Identity.Application.Contracts.AuthorizationGrants;
namespace Refahi.Modules.Pools.Tests;
// Doubles belong exclusively to tests; production registers only PoolTicketProvider.
public sealed class TestClock:TimeProvider { public DateTimeOffset? Now; public override DateTimeOffset GetUtcNow()=>Now??DateTimeOffset.UtcNow; }
public sealed class GrantState { public Guid User,Pool;public bool Active=true; }
public sealed class GrantHandler(GrantState state):IRequestHandler<GetActiveAuthorizationGrantsQuery,IReadOnlyList<AuthorizationGrantDto>>
{
 public Task<IReadOnlyList<AuthorizationGrantDto>> Handle(GetActiveAuthorizationGrantsQuery r,CancellationToken ct)=>Task.FromResult<IReadOnlyList<AuthorizationGrantDto>>(state.Active&&state.User==r.UserId?[new(Guid.NewGuid(),r.UserId,"Pools",$"pool:{state.Pool:N}:redeem",null,true,DateTimeOffset.UtcNow,null,null,null)]:[]);
}
public sealed class TestPoolProvider:IPoolProvider
{
 public string Key=>"poolticket";public string Name=>"test";public PoolCapabilities Capabilities=>new(true,true,false,false,false,false,false);
 public long PriceDelta;public bool FailSessions,OnlyOne,MultiPage,FailPageTwo;public string? FailPoolId;
 public string Date{get;}=DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd");
 public Task<IReadOnlyList<PoolState>> StatesAsync(bool active,CancellationToken ct)=>Task.FromResult<IReadOnlyList<PoolState>>([]);
 public Task<IReadOnlyList<PoolCity>> CitiesAsync(string? state,bool active,CancellationToken ct)=>Task.FromResult<IReadOnlyList<PoolCity>>([]);
 public Task<PoolCatalog> SearchAsync(PoolSearchQuery q,CancellationToken ct)
 {
  PoolSummary Pool(string id)=>new("poolticket",id,"استخر "+id,null,"تهران","تهران",null,null,null,true,2,null,null,50000,50000,"TOMAN");
  if(q.Page==2&&FailPageTwo)throw new PoolException("دریافت ناقص",502);
  IReadOnlyList<PoolSummary> items=MultiPage?q.Page==1?Enumerable.Range(100,50).Select(x=>Pool(x.ToString())).ToArray():q.Page==2?[Pool("150")]:[]:q.Page==1?[Pool("12")]:[];
  return Task.FromResult(new PoolCatalog(items,q.Page,q.Limit,[],null));
 }
 public Task<IReadOnlyList<PoolSession>> SessionsAsync(string pool,CancellationToken ct)
 {
  if(FailSessions||pool==FailPoolId)throw new PoolException("دریافت ناقص",502);
  IReadOnlyList<PoolSessionRow> rows=OnlyOne?[Row("1")]:[Row("1"),Row("2")];
  return Task.FromResult<IReadOnlyList<PoolSession>>([new("7","سانس",2,rows)]);
 }
 private PoolSessionRow Row(string id)=>new(id,true,true,true,50000,25000,50000,25000,Date,"روز","10:00 - 12:00","","","TOMAN");
 public Task<string> TermsAsync(string pool,CancellationToken ct)=>Task.FromResult("<p>قوانین</p>");
 public Task<PoolQuote> QuoteAsync(PoolSelection s,CancellationToken ct)
 {
  var total=s.AdultCount*500000L+s.ChildCount*250000L+PriceDelta;
  return Task.FromResult(new PoolQuote(s.SessionRowId,s.AdultCount,s.ChildCount,total/10m,total/10m,total,"TOMAN",Date,"روز","10:00 - 12:00"));
 }
}

public sealed class AllGrantsHandler(GrantState state):IRequestHandler<GetAuthorizationGrantsByIssuerQuery,IReadOnlyList<AuthorizationGrantDto>>
{
 public Task<IReadOnlyList<AuthorizationGrantDto>> Handle(GetAuthorizationGrantsByIssuerQuery r,CancellationToken ct)=>Task.FromResult<IReadOnlyList<AuthorizationGrantDto>>(state.Active?[new(Guid.NewGuid(),Guid.NewGuid(),"Pools",$"pool:{state.Pool:N}:redeem",null,true,DateTimeOffset.UtcNow,null,null,null)]:[]);
}
