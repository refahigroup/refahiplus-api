using System.Text.Json;
using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.ResellerSchedule;

public sealed class ResellerOrderResourceAttributes
{
    [JsonPropertyName("code")]
    public string Code { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; }

    [JsonPropertyName("status_label")]
    public string StatusLabel { get; init; }

    [JsonPropertyName("currency_code")]
    public string CurrencyCode { get; init; }

    [JsonPropertyName("currency_label")]
    public string CurrencyLabel { get; init; }

    [JsonPropertyName("subtotal_amount")]
    public long SubtotalAmount { get; init; }

    [JsonPropertyName("discount_amount")]
    public long DiscountAmount { get; init; }

    [JsonPropertyName("platform_amount")]
    public long PlatformAmount { get; init; }

    [JsonPropertyName("tax_amount")]
    public long TaxAmount { get; init; }

    [JsonPropertyName("total_amount")]
    public long TotalAmount { get; init; }

    [JsonPropertyName("use_wallet")]
    public bool UseWallet { get; init; }

    [JsonPropertyName("wallet_amount")]
    public long WalletAmount { get; init; }

    [JsonPropertyName("gateway_amount")]
    public long GatewayAmount { get; init; }

    [JsonPropertyName("transaction_id")]
    public string? TransactionId { get; init; }

    [JsonPropertyName("payment_url")]
    public string? PaymentUrl { get; init; }

    [JsonPropertyName("payment_authority")]
    public string? PaymentAuthority { get; init; }

    [JsonPropertyName("payment_callback_url")]
    public string? PaymentCallbackUrl { get; init; }

    [JsonPropertyName("gateway_charge_amount")]
    public long? GatewayChargeAmount { get; init; }

    [JsonPropertyName("expires_at")]
    public string? ExpiresAt { get; init; }

    [JsonPropertyName("coupon")]
    public List<JsonElement> Coupon { get; init; }

    [JsonPropertyName("seats")]
    public List<JsonElement> Seats { get; init; }

    [JsonPropertyName("samfa_id")]
    public int? SamfaId { get; init; }

    [JsonPropertyName("samfa_code")]
    public string? SamfaCode { get; init; }

}
