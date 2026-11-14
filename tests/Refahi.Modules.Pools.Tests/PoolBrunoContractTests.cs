using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Infrastructure.Providers.PoolTicket;
using Xunit;
namespace Refahi.Modules.Pools.Tests;
public sealed class PoolBrunoContractTests
{
    private static PoolTicketOptions Settings() => new() { ClientId="client", Username="org-user", Password="org-password",
        UserMobile="09001769450",UserName="Test",UserPassword="customer-password",UserState="1",UserCity="3",CalculateReadOnlyConfirmed=true };
    private static (PoolTicketProvider Provider,PoolTicketClient Client) Create(Handler handler,PoolTicketOptions? settings=null,PoolTicketTokenState? state=null)
    {
        var options=Options.Create(settings??Settings());var client=new PoolTicketClient(new HttpClient(handler){BaseAddress=new("https://example.test")},options,NullLogger<PoolTicketClient>.Instance,state);
        return(new(client,options),client);
    }
    private const string Token="""{"status":1,"access_token":"server-token","refresh_token":"server-refresh","id":1}""";
    private const string Reservation="""{"status":1,"reservation_status":"reserved","reservation":{"reservation_id":7,"pool_name":"Pool","saens_name":"Session","user_paid":100,"start_date":"1405/08/14","start_day":"day","time":"10:00","count_adult":1,"count_child":0,"gender":0,"voucher":123456,"enter":0,"cancelable":false}}""";
    [Fact]
    public async Task Organization_token_is_shared_across_concurrent_clients_and_check_uses_documented_grant()
    {
        var handler=new Handler((path,body)=>path=="/api/v1/token" ? body["grant_type"]=="password"?Token:"{\"status\":1}":"{\"status\":true,\"states\":[]}");
        var state=new PoolTicketTokenState();
        await Task.WhenAll(Enumerable.Range(0,8).Select(_=>Create(handler,state:state).Provider.StatesAsync(false,default)));
        await Create(handler,state:state).Client.CheckTokenAsync(default);
        Assert.Single(handler.Requests,x=>x.Body.GetValueOrDefault("grant_type")=="password");
        var login=handler.Requests.Single(x=>x.Body.GetValueOrDefault("grant_type")=="password");
        Assert.Equal("organization",login.Body["scope"]);Assert.Equal("org-user",login.Body["username"]);
        var check=handler.Requests.Single(x=>x.Body.GetValueOrDefault("grant_type")=="check_token");Assert.Equal("server-refresh",check.Body["refresh_token"]);
        Assert.All(handler.Requests.Where(x=>x.Path.Contains("/org/")),x=>Assert.Equal("server-token",x.Body["access_token"]));
    }
    [Fact]
    public async Task Expired_read_token_is_reacquired_once_but_reserve_is_never_replayed_for_auth_errors()
    {
        var settings=Settings();settings.AccessToken="expired";
        var handler=new Handler((path,body)=>path=="/api/v1/token"?Token:body["access_token"]=="expired"?"{\"status\":0,\"error\":\"expired_access_token\"}":"{\"status\":1,\"states\":[]}");
        await Create(handler,settings).Provider.StatesAsync(false,default);Assert.Equal(3,handler.Requests.Count);
        handler=new Handler((_,_)=>"{\"status\":0,\"error\":\"invalid_access_token\"}");
        await Assert.ThrowsAsync<PoolException>(()=>Create(handler,settings).Provider.ReserveAsync(new("poolticket","1","9",1,0),default));
        Assert.Single(handler.Requests);
    }
    [Fact]
    public async Task Explicit_missing_customer_registers_configured_user_then_reserves_once()
    {
        var attempts=0;var handler=new Handler((path,body)=>path switch {
            "/api/v1/token"=>Token,
            "/api/v1/org/auth/register"=>"{\"status\":1}",
            "/api/v1/org/reservation/reserve"=>Interlocked.Increment(ref attempts)==1?"{\"status\":0,\"error\":\"not_found_user\"}":Reservation,
            _=>throw new InvalidOperationException(path)});
        var r=await Create(handler).Provider.ReserveAsync(new("poolticket","1","9",1,0),default);Assert.Equal("123456",r.Voucher);Assert.Equal("7",r.Id);
        var registration=handler.Requests.Single(x=>x.Path.EndsWith("auth/register"));
        Assert.Equal("Test",registration.Body["name"]);Assert.Equal("customer-password",registration.Body["password"]);
        Assert.Equal("1",registration.Body["state_id"]);Assert.Equal("3",registration.Body["city_id"]);Assert.DoesNotContain("code_meli",registration.Body.Keys);
        Assert.All(handler.Requests.Where(x=>x.Path.EndsWith("reserve")||x.Path.EndsWith("register")),x=>Assert.Equal("09001769450",x.Body["mobile"]));
        Assert.Equal(2,attempts);
    }
    [Fact]
    public async Task Ambiguous_reserve_response_is_never_retried()
    {
        var settings=Settings();settings.AccessToken="token";var handler=new Handler((_,_)=>throw new HttpRequestException());
        await Assert.ThrowsAsync<PoolException>(()=>Create(handler,settings).Provider.ReserveAsync(new("poolticket","1","9",1,0),default));Assert.Single(handler.Requests);
    }
    [Fact]
    public async Task Areas_session_info_history_and_reservation_info_match_collection_paths_and_parameters()
    {
        var handler=new Handler((path,body)=>path switch {
            "/api/v1/token"=>Token,
            "/api/v1/org/area/list"=>"""{"status":true,"areas":[{"area_id":2,"city_id":3,"area_name":"Area"}]}""",
            "/api/v1/org/saens/info"=>"""{"status":1,"saens_row":{"saens_row_id":9,"saens_name":"Session","remain_capacity":true,"status_sell":true,"start_datetime":"2026-11-05 10:00:00","finish_datetime":"2026-11-05 12:00:00"}}""",
            "/api/v1/org/reservation/info"=>Reservation,
            "/api/v1/org/reservation/history/1/10"=>"{\"status\":1,\"page\":1,\"limit\":\"10\",\"reservations\":["+JsonDocument.Parse(Reservation).RootElement.GetProperty("reservation").GetRawText()+"]}",
            _=>throw new InvalidOperationException(path)});
        var p=Create(handler).Provider;Assert.Equal("3",(await p.AreasAsync("3",default)).Single().CityId);
        Assert.True((await p.SessionInfoAsync("1","9",default)).Available);
        Assert.Equal("7",(await p.ReservationInfoAsync("7",default)).Id);Assert.Single((await p.ReservationHistoryAsync(1,10,default)).Items);
        Assert.Equal("1",handler.Requests.Single(x=>x.Path.EndsWith("saens/info")).Body["pool"]);
        Assert.All(handler.Requests.Where(x=>x.Path.Contains("reservation/")),x=>Assert.Equal("09001769450",x.Body["mobile"]));
    }
    [Fact]
    public async Task Search_accepts_empty_coordinates_string_discount_and_total_pages_without_public_only_filters()
    {
        var handler=new Handler((path,body)=>path=="/api/v1/token"?Token:"""{"status":1,"page":1,"limit":10,"total_pages":1,"pools":[{"pool_id":1,"name":"Pool","state_id":2,"city_id":3,"lat":"","lng":"","max_discount":"15","status_sell":1}]}""");
        var result=await Create(handler).Provider.SearchAsync(new("poolticket",CityId:"3",AreaId:"2",Gender:1),default);
        Assert.Null(result.Items.Single().Latitude);Assert.Equal(15,result.Items.Single().MaxDiscount);Assert.Equal(1,result.TotalPages);
        var request=handler.Requests.Last();Assert.Equal("/api/v1/org/pool/list/1/10",request.Path);Assert.Equal("3",request.Body["city"]);
        Assert.DoesNotContain("area_id",request.Body.Keys);Assert.DoesNotContain("gender",request.Body.Keys);Assert.DoesNotContain("sort_by",request.Body.Keys);
    }
    [Theory]
    [InlineData("true",true)][InlineData("false",false)][InlineData("4000",true)][InlineData("0",false)]
    public async Task Session_info_accepts_documented_boolean_and_observed_numeric_capacity_without_inventing_count(string capacity,bool available)
    {
        var handler=new Handler((path,body)=>path=="/api/v1/token"?Token:
            "{\"status\":1,\"saens_row\":{\"saens_row_id\":9,\"remain_capacity\":"+capacity+",\"status_sell\":true,\"start_datetime\":\"2026-11-05 10:00:00\",\"finish_datetime\":\"2026-11-05 12:00:00\"}}");
        Assert.Equal(available,(await Create(handler).Provider.SessionInfoAsync("1","9",default)).Available);
    }
    private sealed class Handler(Func<string,Dictionary<string,string>,string> respond):HttpMessageHandler
    {
        public List<(string Path,Dictionary<string,string> Body)> Requests {get;}=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct)
        {
            var form=(await r.Content!.ReadAsStringAsync(ct)).Split('&').Select(x=>x.Split('=',2)).ToDictionary(x=>WebUtility.UrlDecode(x[0]),x=>WebUtility.UrlDecode(x[1]));
            lock(Requests)Requests.Add((r.RequestUri!.AbsolutePath,form));
            return new(HttpStatusCode.OK){Content=new StringContent(respond(r.RequestUri!.AbsolutePath,form),Encoding.UTF8,"application/json")};
        }
    }
}
