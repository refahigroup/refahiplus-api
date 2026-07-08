using Refahi.Modules.Charge.Application.Contracts.Providers;
using Refahi.Modules.Charge.Domain.Enums;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Refahi.Modules.Charge.Infrastructure.Providers.Eniac;

public sealed class EniacChargeProvider : IChargeProvider
{
    private readonly EniacApiClient _api;
    public EniacChargeProvider(EniacApiClient api) => _api = api;
    public string Name => "Eniac";

    public async Task<IReadOnlyList<ChargeProductDto>> GetProductsAsync(ChargeOperator op, CancellationToken ct)
    {
        using var doc = await _api.GetAsync($"/api/Operator/GetOperatorProducts/{(short)op}", ct);
        return MapProducts(doc.RootElement, op, false);
    }

    public async Task<IReadOnlyList<ChargeProductDto>> GetOffersAsync(ChargeOperator op, string mobile, ChargeOfferCategory category, CancellationToken ct)
    {
        if (op == ChargeOperator.Irancell)
        {
            var code = category switch { ChargeOfferCategory.Hourly => 1, ChargeOfferCategory.Daily => 2, ChargeOfferCategory.Weekly => 3, ChargeOfferCategory.Monthly => 4, _ => 0 };
            using var doc = await _api.PostAsync("/api/Merchant/GetOfferListIrancell", new { destinationMobileNumber = mobile, category = code }, true, ct);
            return MapProducts(doc.RootElement, op, true);
        }
        if (op == ChargeOperator.Mci)
        {
            var code = category switch { ChargeOfferCategory.Voice => 1, ChargeOfferCategory.Internet => 2, ChargeOfferCategory.Sms => 3, _ => 321 };
            using var doc = await _api.PostAsync("/api/Merchant/GetOfferListMCI", new { destinationMobileNumber = mobile, bizType = code }, true, ct);
            return MapProducts(doc.RootElement, op, true);
        }
        return [];
    }

    public async Task<ChargeEligibilityDto> CheckEligibilityAsync(ChargeEligibilityRequest r, CancellationToken ct)
    {
        if (r.Operator is not (ChargeOperator.Irancell or ChargeOperator.Mci)) 
            return new(false, null, null, 0, null, null);

        var path = r.Operator == ChargeOperator.Irancell 
            ? "/api/Merchant/CheckOrderIrancell" : 
            "/api/Merchant/CheckOrderMCI";

        var data = new 
        { 
            destinationMobileNumber = r.DestinationMobileNumber, 
            amount = r.AmountMinor, 
            operatorProductId = r.ProviderProductId, 
            productCategory = r.ProductCategory 
        };

        using var doc = await _api.PostAsync(path, data, true, ct);

        var root = doc.RootElement; 
        var success = EniacJson.Bool(root, "success");

        return new(
            true, 
            success, 
            EniacJson.String(root, "data"), 
            EniacJson.Int(root, "eniacResultCode"),
            EniacJson.String(root, "operatorResaultCode", "operatorResultCode"), 
            EniacJson.String(root, "message")
        );
    }

    public async Task<ChargePostpaidBalanceDto> GetPostpaidBalanceAsync(ChargeOperator op, string mobile, CancellationToken ct)
    {
        if (op != ChargeOperator.Irancell) 
            throw new InvalidOperationException("استعلام قبض فقط برای ایرانسل پشتیبانی می‌شود");

        using var doc = await _api.PostAsync("/api/Merchant/GetPostpaidBalanceIrancell", new { destinationMobileNumber = mobile }, true, ct);

        var data = EniacJson.Data(doc.RootElement);

        return new(
            EniacJson.String(data, "correlation_id") ?? string.Empty, 
            EniacJson.Long(data, "current_balance"),
            EniacJson.Long(data, "outstanding_balance"), 
            EniacJson.Int(data, "result_code"), 
            EniacJson.String(data, "result_message")
        );
    }

    public async Task<IReadOnlyList<PinChargeCategoryDto>> GetPinCategoriesAsync(CancellationToken ct)
    {
        using var doc = await _api.GetAsync("/api/Operator/GetPinChargeCategory", ct); var data = EniacJson.Data(doc.RootElement);
        if (data.ValueKind != JsonValueKind.Array) return [];
        return data.EnumerateArray().Select(x => new PinChargeCategoryDto(EniacJson.Int(x, "pinChargeCategroy"),
            EniacJson.String(x, "codeValue") ?? string.Empty, EniacJson.Long(x, "amount"), EniacJson.String(x, "describe") ?? string.Empty,
            (ChargeOperator)(EniacJson.Int(x, "pinChargeCategroy") / 1000))).ToList();
    }

    public async Task<IReadOnlyList<PackageTypeDto>> GetPackageTypesAsync(CancellationToken ct)
    {
        using var doc = await _api.GetAsync("/api/Operator/GetPackageType", ct); var data = EniacJson.Data(doc.RootElement);
        if (data.ValueKind != JsonValueKind.Array) return [];
        return data.EnumerateArray().Select(x => new PackageTypeDto(EniacJson.String(x, "title") ?? string.Empty,
            EniacJson.String(x, "titleFa"), EniacJson.Int(x, "packageTypeCode"),
            (ChargeOperator)EniacJson.Int(x, "operatorTypeId", "operatorTypesId"),
            EniacJson.String(x, "operatorTypeName", "operatorTypesName") ?? string.Empty)).ToList();
    }

    public async Task<IReadOnlyList<ProviderChannelDto>> GetChannelsAsync(CancellationToken ct)
    {
        using var doc = await _api.GetAsync("/api/Operator/EnumOperatorChannelTypes", ct); var data = EniacJson.Data(doc.RootElement);
        return data.ValueKind == JsonValueKind.Array ? data.EnumerateArray().Select(x =>
            new ProviderChannelDto(EniacJson.Int(x, "channelId"), EniacJson.String(x, "channelName") ?? string.Empty)).ToList() : [];
    }

    public async Task<ProviderPurchaseResultDto> PurchaseAsync(ProviderPurchaseRequest r, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew(); JsonDocument doc;
        object body; string path;
        if (r.ServiceType == ChargeServiceType.PinCharge)
        {
            path = "/api/Merchant/BuyPinCharge";
            body = new { operatorType = (short)r.Operator, pinChargeCategroy = r.PinCategoryId,
                customerInvoiceNumber = r.CustomerInvoiceNumber, channelId = r.ChannelId, count = r.PinCount };
        }
        else
        {
            path = r.Operator switch { ChargeOperator.Irancell => "/api/Merchant/BuyIrancell", ChargeOperator.Mci => "/api/Merchant/BuyMCI",
                ChargeOperator.Rightel => "/api/Merchant/BuyRighTel", ChargeOperator.Shatel => "/api/Merchant/BuyShatel",
                ChargeOperator.Taliya => "/api/Merchant/BuyTaliya", _ => throw new InvalidOperationException("اپراتور پشتیبانی نمی‌شود") };
            body = r.Operator == ChargeOperator.Irancell
                ? new { origenMobileNumber = r.OriginMobileNumber, destinationMobileNumber = r.DestinationMobileNumber,
                    amount = r.AmountMinor, customerInvoiceNumber = r.CustomerInvoiceNumber, operatorProductId = r.ProviderProductId,
                    productCategory = r.ProductCategory, paybill = r.PayBill, channelId = r.ChannelId, resellerName = r.ResellerName }
                : new { origenMobileNumber = r.OriginMobileNumber, destinationMobileNumber = r.DestinationMobileNumber,
                    amount = r.AmountMinor, customerInvoiceNumber = r.CustomerInvoiceNumber, operatorProductId = r.ProviderProductId,
                    productCategory = r.ProductCategory, channelId = r.ChannelId };
        }
        using (doc = await _api.PostAsync(path, body, false, ct))
        {
            sw.Stop(); var root = doc.RootElement; var data = EniacJson.Data(root); var pins = ReadPins(data);
            return new(EniacJson.Bool(root, "success"), EniacJson.Int(root, "eniacResultCode"),
                EniacJson.String(root, "operatorResaultCode", "operatorResultCode"), EniacJson.String(root, "message"),
                EniacJson.String(data, "rrn"), EniacJson.String(data, "customerInvoiceNumber"),
                EniacJson.String(data, "operatorTraceId"), pins,
                JsonSerializer.Serialize(new { r.Operator, r.ServiceType, destination = Mask(r.DestinationMobileNumber), r.AmountMinor, r.CustomerInvoiceNumber, r.ProviderProductId }),
                JsonSerializer.Serialize(new { success = EniacJson.Bool(root, "success"), eniacResultCode = EniacJson.Int(root, "eniacResultCode"),
                    operatorResultCode = EniacJson.String(root, "operatorResaultCode", "operatorResultCode"), rrn = EniacJson.String(data, "rrn"), pinCount = pins.Count }),
                sw.ElapsedMilliseconds);
        }
    }

    public async Task<ProviderTraceResultDto> TraceAsync(ProviderTraceRequest r, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var body = new { eniacRRN = r.EniacRrn, customerInvoiceNumber = r.CustomerInvoiceNumber,
            operatorPackageId = r.ProviderProductId, amount = r.AmountMinor, date = ToPersianDate(r.Date) };
        using var doc = await _api.PostAsync("/api/Report/TraceTransaction", body, true, ct);
        sw.Stop(); var root = doc.RootElement; var data = EniacJson.Data(root); var pins = ReadPins(data);
        return new(EniacJson.Bool(root, "success"), EniacJson.Int(root, "eniacResultCode"),
            EniacJson.String(data, "operatorResaultCode", "operatorResultCode"), EniacJson.String(root, "message", "eniacResultMessage"),
            EniacJson.String(data, "rrn"), EniacJson.String(data, "operatorTrackingCode"),
            EniacJson.NullableInt(data, "paymentResultCode"), EniacJson.NullableInt(data, "reverseResultCode"), pins,
            JsonSerializer.Serialize(new { r.CustomerInvoiceNumber, r.ProviderProductId, r.AmountMinor, date = ToPersianDate(r.Date) }),
            JsonSerializer.Serialize(new { success = EniacJson.Bool(root, "success"), eniacResultCode = EniacJson.Int(root, "eniacResultCode"),
                rrn = EniacJson.String(data, "rrn"), paymentResultCode = EniacJson.String(data, "paymentResultCode"), pinCount = pins.Count }), sw.ElapsedMilliseconds);
    }

    public async Task<ProviderBalanceDto> GetBalanceAsync(CancellationToken ct)
    {
        using var doc = await _api.GetAsync("/api/Wallet/GetBallance", ct); var data = EniacJson.Data(doc.RootElement);
        return new(EniacJson.Long(data, "totalBalance"), EniacJson.Long(data, "suspendedBalance"),
            EniacJson.Long(data, "availableBalance"), EniacJson.Bool(data, "isTotalBalanceConfidentiality"));
    }
    public async Task<IReadOnlyList<ProviderErrorDto>> GetErrorsAsync(CancellationToken ct)
    {
        using var doc = await _api.GetAsync("/api/Operator/ErrorList", ct); var data = EniacJson.Data(doc.RootElement);
        return data.ValueKind == JsonValueKind.Array ? data.EnumerateArray().Select(x => new ProviderErrorDto(
            EniacJson.Int(x, "eniacResultCode"), EniacJson.String(x, "message") ?? string.Empty)).ToList() : [];
    }
    public Task<ProviderReportDto> GetTransactionReportAsync(ProviderReportRequest r, CancellationToken ct)
        => ReportAsync("/api/Report/ReportTransaction", r, ct);
    public Task<ProviderReportDto> GetWalletChargeReportAsync(ProviderReportRequest r, CancellationToken ct)
        => ReportAsync("/api/Report/ReportWalletCharge", r, ct);
    private async Task<ProviderReportDto> ReportAsync(string path, ProviderReportRequest r, CancellationToken ct)
    {
        using var doc = await _api.PostAsync(path, new { pageNumber = Math.Max(1, r.PageNumber),
            fromDate = r.FromDate.HasValue ? ToPersianDate(r.FromDate.Value) : null,
            toDate = r.ToDate.HasValue ? ToPersianDate(r.ToDate.Value) : null }, true, ct);
        var root = doc.RootElement; var data = EniacJson.Data(root);
        return new(data.ValueKind == JsonValueKind.Undefined ? "null" : data.GetRawText(), EniacJson.Int(root, "totalCount"),
            EniacJson.Bool(root, "success"), EniacJson.Int(root, "eniacResultCode"), EniacJson.String(root, "message"));
    }

    private static IReadOnlyList<ChargeProductDto> MapProducts(JsonElement root, ChargeOperator op, bool personalized)
    {
        var data = EniacJson.Data(root); if (data.ValueKind != JsonValueKind.Array) return [];
        return data.EnumerateArray().Select(x => new ChargeProductDto(EniacJson.String(x, "operatorPackageId") ?? string.Empty,
            EniacJson.String(x, "titleFa", "titelEn", "titleEn") ?? string.Empty, EniacJson.String(x, "titleDetail"),
            EniacJson.Int(x, "packageTypeCode"), EniacJson.String(x, "packageTypeName"), op,
            EniacJson.Long(x, "amount"), EniacJson.Long(x, "amountWithTax"), EniacJson.String(x, "capacity"),
            EniacJson.Int(x, "durationDays"), EniacJson.Bool(x, "isActive"), EniacJson.NullableInt(x, "packageCategoryId"),
            EniacJson.String(x, "packageCategoryName"), personalized)).ToList();
    }
    private static IReadOnlyList<ProviderPinDto> ReadPins(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("pinChargeList", out var pins) || pins.ValueKind != JsonValueKind.Array) return [];
        return pins.EnumerateArray().Select(x => new ProviderPinDto(EniacJson.String(x, "pinChargeSerial", "serial") ?? string.Empty,
            EniacJson.String(x, "pinChargeCode", "pin") ?? string.Empty, EniacJson.Long(x, "amnount", "amount"))).ToList();
    }
    private static string ToPersianDate(DateOnly date)
    {
        var pc = new PersianCalendar(); var dt = date.ToDateTime(TimeOnly.MinValue);
        return $"{pc.GetYear(dt):0000}-{pc.GetMonth(dt):00}-{pc.GetDayOfMonth(dt):00}";
    }
    private static string Mask(string value) => value.Length < 7 ? "***" : $"{value[..4]}***{value[^4..]}";
}
