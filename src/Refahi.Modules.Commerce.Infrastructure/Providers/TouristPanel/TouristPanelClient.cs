using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.Commerce.Application.Contracts.Providers.Exceptions;
using Refahi.Modules.Commerce.Infrastructure.Providers.TouristPanel.Contracts;

namespace Refahi.Modules.Commerce.Infrastructure.Providers.TouristPanel;

public sealed class TouristPanelClient(HttpClient http, TouristPanelTokenProvider tokens,
    IOptions<TouristPanelOptions> options, ILogger<TouristPanelClient> logger)
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private const string Hub = "/api/v3/marketplace/services/hub/";
    public Task<TpGisheSettingsDto> GetSettingsAsync(CancellationToken ct) => Send<TpGisheSettingsDto>("gishe-settings", HttpMethod.Get, [], null, ct);
    public Task<string> GetNativeVersionAsync(CancellationToken ct) => Send<string>("native-app-version", HttpMethod.Get, [], null, ct);
    public Task<List<TpLocationDto>> GetLocationsAsync(CancellationToken ct) => Send<List<TpLocationDto>>("locations", HttpMethod.Get, [], null, ct);
    public Task<List<TpProgramCategoryDto>> GetCategoriesAsync(string? location, CancellationToken ct) => Send<List<TpProgramCategoryDto>>("categories", HttpMethod.Get, [("locationCode", location)], null, ct);
    public Task<List<TpProgramListDto>> GetProgramsAsync(int page, int take, string? location, int[]? categories, CancellationToken ct) =>
        Send<List<TpProgramListDto>>("programs", HttpMethod.Get, new[] { ("page", page.ToString()), ("take", take.ToString()), ("locationCode", location) }
            .Concat((categories ?? []).Select(x => ("categoryCodes", (string?)x.ToString()))), null, ct);
    public Task<TpProgramDto> GetProgramAsync(string id, CancellationToken ct) => Send<TpProgramDto>("program", HttpMethod.Get, [("programId", Id(id))], null, ct);
    public Task<TpProgramDto> GetDetailAsync(string id, string[]? types, CancellationToken ct) => Send<TpProgramDto>("program/detail", HttpMethod.Get,
        new[] { ("programId", (string?)Id(id)) }.Concat(Types(types)), null, ct);
    public Task<List<TpTicketTypeDto>> GetProgramTicketsAsync(string id, string seller, CancellationToken ct) =>
        Send<List<TpTicketTypeDto>>("program/ticket-types", HttpMethod.Get, [("programId", Id(id)), ("supplyChainHojreId", Id(seller))], null, ct);
    public Task<List<TpEventSansDto>> GetEventsAsync(string id, string seller, DateOnly? start, DateOnly? end, string[]? types, bool includeTypes, CancellationToken ct) =>
        Send<List<TpEventSansDto>>("events", HttpMethod.Get, new[] { ("programId", (string?)Id(id)), ("supplyChainHojreId", Id(seller)),
            ("start", start?.ToString("yyyy-MM-dd")), ("end", end?.ToString("yyyy-MM-dd")), ("icloudTicketTypes", includeTypes ? "true" : "false") }.Concat(Types(types)), null, ct);
    public Task<List<TpTicketTypeDto>> GetEventTicketsAsync(string id, string seller, string[]? types, CancellationToken ct) =>
        Send<List<TpTicketTypeDto>>("event/ticket-types", HttpMethod.Get, new[] { ("eventId", (string?)Id(id)), ("supplyChainHojreId", Id(seller)) }.Concat(Types(types)), null, ct);
    public Task<TpTempShoppingCartDto> AddGroupAsync(TpAddTempGroupDto group, string? cart, string? groupId, CancellationToken ct) =>
        Send<TpTempShoppingCartDto>("shopping-cart", HttpMethod.Post, [("shoppingCartId", OptionalId(cart)), ("groupId", OptionalId(groupId))], group, ct);
    public Task<TpTempShoppingCartDto> AddGroupsAsync(IReadOnlyList<TpAddTempGroupDto> groups, string? cart, CancellationToken ct) =>
        Send<TpTempShoppingCartDto>("shopping-cart-groups", HttpMethod.Post, [("shoppingCartId", OptionalId(cart))], groups, ct);
    public Task<TpTempShoppingCartDto> GetCartAsync(string id, CancellationToken ct) => Send<TpTempShoppingCartDto>("shopping-cart", HttpMethod.Get, [("shoppingCartId", Id(id))], null, ct);
    public Task<bool> DeleteCartAsync(string id, CancellationToken ct) => Delete(id, null, null, ct);
    public Task<bool> DeleteGroupAsync(string cart, string group, CancellationToken ct) => Delete(cart, Id(group), null, ct);
    public Task<bool> DeleteTicketAsync(string cart, string ticket, CancellationToken ct) => Delete(cart, null, Id(ticket), ct);
    private Task<bool> Delete(string cart, string? group, string? ticket, CancellationToken ct) =>
        Send<bool>("shopping-cart", HttpMethod.Delete, [("shoppingCartId", Id(cart)), ("groupId", group), ("ticketId", ticket)], null, ct);
    public Task<TpPurchaseResultDto> FinalizeAsync(string cart, TpFinalizingTempShoppingCartDto body, string? desk, string? discount, CancellationToken ct) =>
        Send<TpPurchaseResultDto>("finalizing-shopping-cart", HttpMethod.Patch, [("shoppingCartId", Id(cart)), ("cashDeskId", OptionalId(desk)), ("discountCode", discount)], body, ct);
    public Task<TpDiscountValidationResult> CheckDiscountAsync(string cart, string discount, TpFinalizingTempShoppingCartDto body, CancellationToken ct) =>
        Send<TpDiscountValidationResult>("check-discount-code", HttpMethod.Get, [("shoppingCartId", Id(cart)), ("discountCode", discount)], body, ct);
    public Task<TpPurchaseResultDto> GetPurchaseAsync(string invoice, CancellationToken ct) =>
        Send<TpPurchaseResultDto>("purchase-result", HttpMethod.Get, [("invoiceId", Id(invoice))], null, ct);

    private static IEnumerable<(string, string?)> Types(string[]? types) => (types ?? []).Select(x => ("filteredProgramTicketTypes", (string?)Id(x)));
    private static string? OptionalId(string? id) => id is null ? null : Id(id);
    private static string Id(string id) => Guid.TryParse(id, out var value) && value != Guid.Empty ? value.ToString() : throw new ArgumentException("شناسه توریست‌پنل معتبر نیست");

    private async Task<T> Send<T>(string operation, HttpMethod method, IEnumerable<(string Key, string? Value)> query, object? body, CancellationToken ct)
    {
        var suffix = string.Join('&', query.Where(x => !string.IsNullOrWhiteSpace(x.Value)).Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value!)}"));
        var uri = options.Value.BaseUrl.TrimEnd('/') + Hub + operation + (suffix.Length == 0 ? "" : "?" + suffix);
        var bearer = await tokens.GetAsync(ct);
        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(method, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            request.Headers.Add("__tenant", options.Value.Tenant);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (body is not null) request.Content = JsonContent.Create(body, options: Json);
            var watch = Stopwatch.StartNew();
            try
            {
                using var response = await http.SendAsync(request, ct);
                logger.LogInformation("TouristPanel {Operation} HTTP {Status} in {ElapsedMs}ms", operation, (int)response.StatusCode, watch.ElapsedMilliseconds);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    tokens.Invalidate(bearer);
                    if (method == HttpMethod.Get && attempt == 0) { bearer = await tokens.GetAsync(ct); continue; }
                }
                if (!response.IsSuccessStatusCode)
                {
                    if (method != HttpMethod.Get && (int)response.StatusCode >= 500)
                        throw new CommerceProviderAmbiguousException("نتیجه عملیات توریست‌پنل مشخص نیست");
                    throw new TouristPanelHttpException((int)response.StatusCode, "درخواست توریست‌پنل پذیرفته نشد",
                        TouristPanelFailureKind.ProviderResponse);
                }
                var raw = await response.Content.ReadAsStringAsync(ct);
                var failure = string.IsNullOrWhiteSpace(raw) ? TouristPanelFailureKind.EmptyBody
                    : response.Content.Headers.ContentType?.MediaType?.Contains("html", StringComparison.OrdinalIgnoreCase) == true
                        || raw.AsSpan().TrimStart().StartsWith("<") ? TouristPanelFailureKind.Html
                        : TouristPanelFailureKind.MalformedJson;
                try
                {
                    if (failure is TouristPanelFailureKind.EmptyBody or TouristPanelFailureKind.Html) throw new JsonException();
                    return JsonSerializer.Deserialize<T>(raw, Json) ?? throw new JsonException();
                }
                catch (Exception ex) when (ex is JsonException or NotSupportedException)
                {
                    var invalid = new TouristPanelHttpException(502, "پاسخ توریست‌پنل معتبر نیست", failure);
                    if (method != HttpMethod.Get) throw new CommerceProviderAmbiguousException("پاسخ عملیات توریست‌پنل قابل تطبیق نیست", invalid);
                    throw invalid;
                }
            }
            catch (Exception ex) when (method != HttpMethod.Get && ex is HttpRequestException or OperationCanceledException)
            { throw new CommerceProviderAmbiguousException("نتیجه عملیات توریست‌پنل مشخص نیست"); }
        }
    }
}
