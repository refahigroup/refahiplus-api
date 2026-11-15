using System.Net;
using Microsoft.Extensions.Options;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Infrastructure.Providers.Aabsar.Abstraction;
using Refahi.Modules.Pools.Infrastructure.Providers.Aabsar.Dtos;
using Refahi.Modules.Pools.Infrastructure.Providers.Aabsar.Options;

namespace Refahi.Modules.Pools.Infrastructure.Providers.Aabsar;

internal sealed class AabsarProvider(IAabsarApiClient api, IOptionsMonitor<AabsarOptions> options) : IAabsarGateway
{
    public AabsarSaleOptions Options => new(options.CurrentValue.PurchaseEnabled, options.CurrentValue.RecipientRetentionDays);

    public async Task<AabsarLiveQuote> QuoteAsync(string showtimeId, CancellationToken ct)
    {
        try
        {
            var response = await api.CheckShowtimeAsync(new() { ShowtimeId = showtimeId }, ct);
            var current = response.Data;
            if (response.StatusCode is < 200 or >= 300 || current is null || current.Id != showtimeId || current.Capacity < 0 ||
                current.AdultPrice < 0 || current.ChildPrice < 0)
                throw new PoolException("استعلام سانس آبسار معتبر نیست", 502);
            if (!string.Equals(current.Status, "active", StringComparison.OrdinalIgnoreCase))
                throw new PoolException("سانس انتخاب‌شده در دسترس نیست", 409, "SESSION_UNAVAILABLE");
            return new(current.Capacity, current.AdultPrice, current.ChildPrice, current.AdultOldPrice, current.ChildOldPrice);
        }
        catch (AabsarApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity)
        { throw new PoolException("سانس انتخاب‌شده در دسترس نیست", 409, "SESSION_UNAVAILABLE"); }
        catch (HttpRequestException) { throw new PoolException("استعلام آبسار موقتاً در دسترس نیست", 502); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new PoolException("مهلت استعلام آبسار پایان یافت", 502); }
    }

    public async Task<AabsarReceipt?> IssueAsync(AabsarContact contact, IReadOnlyList<AabsarSelection> selections, CancellationToken ct)
    {
        var digits = new string(contact.MobileNumber!.Where(char.IsAsciiDigit).ToArray());
        if (digits.StartsWith("98", StringComparison.Ordinal) && digits.Length == 12) digits = digits[2..];
        digits = digits.TrimStart('0');
        AabsarCreateOrderResultDto? result;
        try
        {
            var response = await api.CreateOrderAsync(new()
            {
                User = new AabsarCreateOrderUser { FullName = contact.FullName, Phone = digits },
                Items = selections.Select(x => new AabsarCreateOrderItem
                {
                    ShowtimeId = x.ShowtimeId, AdultQuantity = x.AdultCount, ChildQuantity = x.ChildCount
                }).ToArray()
            }, ct);
            if (response.StatusCode is < 200 or >= 300) throw new InvalidOperationException("نتیجه صدور آبسار نامشخص است");
            result = response.Data;
        }
        catch (AabsarApiException ex) when (ex.StatusCode is HttpStatusCode.UnprocessableEntity)
        { return null; }

        if (result is null) throw new InvalidOperationException("پاسخ صدور آبسار نامشخص است");
        return new(result.OrderCode, result.Tickets.Select(x => new AabsarTicket(Guid.NewGuid(), x.IsChild, x.TicketCode)).ToArray(), result.TotalPrice);
    }

    public async Task<bool> CancelAsync(string orderCode, int adults, int children, CancellationToken ct)
    {
        try
        {
            var response = await api.CancelTicketsAsync(new() { OrderCode = orderCode, Adult = adults, Child = children }, ct);
            if (response.StatusCode is < 200 or >= 300 || response.Data is { ValueKind: not System.Text.Json.JsonValueKind.Null })
                throw new InvalidOperationException("نتیجه لغو آبسار نامشخص است");
            return true;
        }
        catch (AabsarApiException ex) when (ex.StatusCode is HttpStatusCode.UnprocessableEntity)
        { return false; }
    }
}
