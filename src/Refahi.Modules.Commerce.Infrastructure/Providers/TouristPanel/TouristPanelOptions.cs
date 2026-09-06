using System.Security.Cryptography;
using System.Text;

namespace Refahi.Modules.Commerce.Infrastructure.Providers.TouristPanel;

public sealed class TouristPanelOptions
{
    public const string SectionName = "Commerce:Providers:TouristPanel";
    public bool Enabled { get; init; }
    public bool SalesEnabled { get; init; }
    public string TokenUrl { get; init; } = "https://id.touristpanel.ir/connect/token";
    public string BaseUrl { get; init; } = "https://marketplace-api.touristpanel.ir";
    public string Tenant { get; init; } = "";
    public string ClientId { get; init; } = "";
    public string ClientSecret { get; init; } = "";
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    public string Scope { get; init; } = "B2CApi";
    public string ImageBaseUrl { get; init; } = "https://files.0tp.ir/";
    public string[] DeliveryHosts { get; init; } = [];
    public int? PaymentMethod { get; init; }
    public int? BankGateway { get; init; }
    public string? CashDeskId { get; init; }
    public bool SettlementConfirmed { get; init; }
    public bool BuyPriceConfirmed { get; init; }
    public bool ManifestConfirmed { get; init; }
    public bool ReferenceTicketsConfirmed { get; init; }
    public bool DateRangeConfirmed { get; init; }
    public string? TimeZoneId { get; init; }
    public string[] AllowedPriceCategoryIds { get; init; } = [];
    public Dictionary<int, string> CategoryCodes { get; init; } = [];
    public decimal? MarkupPercent { get; init; }
    public long? MarkupFixedMinor { get; init; }
    public string PricingVersion { get; init; } = "";
    public int CatalogPageSize { get; init; } = 20;
    public int CatalogMaxPages { get; init; } = 1000;
    public int CatalogRefreshMinutes { get; init; } = 15;
    public int ReservationSafetySeconds { get; init; } = 60;
    public int TimeoutSeconds { get; init; } = 60;
    public string AccountKey => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        $"{BaseUrl}|{Tenant}|{ClientId}|{Username}|{Scope}"))).ToLowerInvariant();

    public bool IsSaleConfigured => SalesEnabled && SettlementConfirmed && BuyPriceConfirmed
        && PaymentMethod.HasValue && BankGateway.HasValue && AllowedPriceCategoryIds.Length > 0
        && MarkupPercent is >= 0 && MarkupFixedMinor is >= 0 && !string.IsNullOrWhiteSpace(PricingVersion)
        && DeliveryHosts.Length > 0;

    public void Validate()
    {
        if (!Enabled) return;
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var api) || api.Scheme != "https"
            || !Uri.TryCreate(TokenUrl, UriKind.Absolute, out var token) || token.Scheme != "https"
            || !Guid.TryParse(Tenant, out _) || new[] { ClientId, ClientSecret, Username, Password, Scope }.Any(string.IsNullOrWhiteSpace)
            || CatalogPageSize is < 1 or > 100 || CatalogMaxPages < 1 || CatalogRefreshMinutes < 1
            || TimeoutSeconds < 1 || ReservationSafetySeconds < 1)
            throw new InvalidOperationException("تنظیمات اتصال توریست‌پنل معتبر نیست");
        if (SalesEnabled && !IsSaleConfigured)
            throw new InvalidOperationException("تأیید قرارداد و تنظیمات فروش توریست‌پنل کامل نیست");
        if (SalesEnabled && (!Enum.IsDefined(typeof(Contracts.TpPaymentMethod), PaymentMethod!.Value)
            || !Enum.IsDefined(typeof(Contracts.TpPaymentGatewayType), BankGateway!.Value)
            || AllowedPriceCategoryIds.Any(x => !Guid.TryParse(x, out var id) || id == Guid.Empty)
            || CategoryCodes.Count == 0 || CategoryCodes.Any(x => string.IsNullOrWhiteSpace(x.Value))
            || DeliveryHosts.Any(x => Uri.CheckHostName(x) == UriHostNameType.Unknown)))
            throw new InvalidOperationException("تنظیمات مالی یا نگاشت فروش توریست‌پنل معتبر نیست");
        if (SalesEnabled && DateRangeConfirmed)
        {
            if (string.IsNullOrWhiteSpace(TimeZoneId))
                throw new InvalidOperationException("منطقه زمانی توریست‌پنل تنظیم نشده است");
            try { _ = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId); }
            catch (TimeZoneNotFoundException) { throw new InvalidOperationException("منطقه زمانی توریست‌پنل معتبر نیست"); }
            catch (InvalidTimeZoneException) { throw new InvalidOperationException("منطقه زمانی توریست‌پنل معتبر نیست"); }
        }
        if (CashDeskId is not null && !Guid.TryParse(CashDeskId, out _))
            throw new InvalidOperationException("شناسه صندوق توریست‌پنل معتبر نیست");
    }
}
