using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Infrastructure.Providers.PoolTicket;
using Xunit;
namespace Refahi.Modules.Pools.Tests;

public sealed class PoolOrganizationSessionTests
{
    private const string Row = """{"saens_row_id":91,"saens_name":"سانس","status":true,"status_sell":1,"adult_sale_status":true,"child_sale_status":false,"remain_capacity":5,"price_sale_adult":100,"price_sale_child":0,"price_market_adult":120,"price_market_child":0,"start_date":"2026-11-05","start_day":"پنجشنبه","time":"10:00","start_datetime":"2026-11-05 10:00:00","finish_datetime":"2026-11-05 12:00:00","terms_of_use":"<p>قانون</p><script>bad()</script>"}""";
    private static PoolTicketProvider Provider(Handler handler, string? token = "org-secret")
    {
        var options = Options.Create(new PoolTicketOptions { ClientId = "client", AccessToken = token });
        return new(new(new HttpClient(handler) { BaseAddress = new("https://example.test/") }, options, NullLogger<PoolTicketClient>.Instance), options);
    }
    private static HttpResponseMessage Response(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private static string Envelope(string row) => "{\"status\":1,\"saens\":[" + row + "]}";
    [Fact]
    public async Task Flat_organization_rows_are_loaded_for_both_genders_with_server_credentials()
    {
        var handler = new Handler(body => Response(Envelope(body.Contains("gender=1") ? Row.Replace("91", "92") : Row)));
        var groups = await Provider(handler).SessionsAsync("72", default);
        Assert.Equal(new[] { 0, 1 }, groups.Select(x => x.Gender));
        Assert.All(groups, g => Assert.Empty(g.Id));
        var row = Assert.Single(groups[0].Rows);
        Assert.Equal("91", row.Id); Assert.True(row.IsSelling); Assert.False(row.ChildEnabled);
        Assert.Equal(new DateTimeOffset(2026, 11, 5, 6, 30, 0, TimeSpan.Zero), row.StartsAtUtc);
        Assert.Equal(TimeSpan.FromHours(2), row.EndsAtUtc - row.StartsAtUtc);
        Assert.DoesNotContain("script", row.TermsHtml);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, r => { Assert.Equal("/api/v1/org/saens/list", r.Path); Assert.Equal("application/json", r.Accept); Assert.Contains("pool=72", r.Body); Assert.DoesNotContain("pool_id", r.Body); Assert.Contains("client_id=client", r.Body); Assert.Contains("access_token=org-secret", r.Body); });
    }
    [Fact]
    public async Task Missing_organization_token_fails_without_network_or_v2_fallback()
    {
        var handler = new Handler(_ => throw new InvalidOperationException());
        var error = await Assert.ThrowsAsync<PoolException>(() => Provider(handler, null).SessionsAsync("72", default));
        Assert.Equal("POOLS_PROVIDER_AUTH_NOT_CONFIGURED", error.Code); Assert.Empty(handler.Requests);
    }
    [Theory]
    [InlineData("invalid_access_token", "POOLS_PROVIDER_TOKEN_INVALID")]
    [InlineData("expired_access_token", "POOLS_PROVIDER_TOKEN_INVALID")]
    [InlineData("access_denied", "POOLS_PROVIDER_ACCESS_DENIED")]
    public async Task Organization_auth_errors_do_not_log_the_refahi_user_out_or_retry(string code, string expected)
    {
        var handler = new Handler(_ => Response(JsonSerializer.Serialize(new { status = 0, error = code }), HttpStatusCode.BadRequest));
        var error = await Assert.ThrowsAsync<PoolException>(() => Provider(handler).SessionsAsync("72", default));
        Assert.Equal(502, error.Status); Assert.Equal(expected, error.Code); Assert.Single(handler.Requests); Assert.DoesNotContain("org-secret", error.Message);
    }
    [Fact]
    public async Task Same_row_returned_for_both_genders_has_one_public_identity()
    {
        var handler = new Handler(_ => Response(Envelope(Row)));
        var group = Assert.Single(await Provider(handler).SessionsAsync("72", default));
        Assert.Equal(2, group.Gender); Assert.Equal("91", Assert.Single(group.Rows).Id);
    }
    [Fact]
    public async Task Conflicting_same_id_across_genders_is_rejected()
    {
        var handler = new Handler(body => Response(Envelope(body.Contains("gender=1") ? Row.Replace("\"price_sale_adult\":100", "\"price_sale_adult\":200") : Row)));
        Assert.Equal(502, (await Assert.ThrowsAsync<PoolException>(() => Provider(handler).SessionsAsync("72", default))).Status);
    }
    [Fact]
    public async Task Duplicate_rows_within_a_gender_are_rejected()
    {
        var handler = new Handler(_ => Response("{\"status\":1,\"saens\":["+Row+","+Row+"]}"));
        Assert.Equal(502, (await Assert.ThrowsAsync<PoolException>(() => Provider(handler).SessionsAsync("72", default))).Status);
    }
    [Theory]
    [InlineData("-1")]
    [InlineData("null")]
    [InlineData("true")]
    public async Task Unknown_or_invalid_numeric_capacity_is_not_invented(string capacity)
    {
        var handler = new Handler(_ => Response(Envelope(Row.Replace("\"remain_capacity\":5", "\"remain_capacity\":" + capacity))));
        Assert.Equal(502, (await Assert.ThrowsAsync<PoolException>(() => Provider(handler).SessionsAsync("72", default))).Status);
    }
    [Fact]
    public async Task Zero_capacity_is_not_sellable()
    {
        var handler = new Handler(body => Response(body.Contains("gender=1") ? "{\"status\":1,\"saens\":[]}" : Envelope(Row.Replace("\"remain_capacity\":5", "\"remain_capacity\":0"))));
        Assert.False(Assert.Single(Assert.Single(await Provider(handler).SessionsAsync("72", default)).Rows).IsSelling);
    }
    [Theory]
    [InlineData("2026-11-05 10:00:00", "2026-11-05 12:00:00")]
    [InlineData("2026-11-05T10:00:00+03:30", "2026-11-05T12:00:00+03:30")]
    [InlineData("2026-11-05T06:30:00Z", "2026-11-05T08:30:00Z")]
    public void Explicit_datetime_pairs_are_used_instead_of_guessing_the_display_time(string start, string end)
    {
        var range = PoolSessionTime.ParseDateTimes(start, end);
        Assert.Equal(new DateTimeOffset(2026, 11, 5, 6, 30, 0, TimeSpan.Zero), range.Start);
        Assert.Equal(TimeSpan.FromHours(2), range.End - range.Start);
    }
    [Theory]
    [InlineData("bad", "bad")]
    [InlineData("2026-11-05 12:00:00", "2026-11-05 10:00:00")]
    [InlineData("2026-11-05 10:00:00", null)]
    public void Invalid_explicit_datetime_pairs_fail_closed(string start, string? end) => Assert.Throws<PoolException>(() => PoolSessionTime.ParseDateTimes(start, end));
    private sealed class Handler(Func<string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(string Path, string Body, string Accept)> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = await request.Content!.ReadAsStringAsync(ct);
            Requests.Add((request.RequestUri!.AbsolutePath, body, request.Headers.Accept.ToString()));
            return respond(body);
        }
    }
}
