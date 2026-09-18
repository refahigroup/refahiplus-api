using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Options;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;

namespace Refahi.Modules.CreditPayments.Application;

public sealed class GatewayPaymentOptions
{
    public const string SectionName = "CreditPayments:GatewayPayment";
    public int DefaultExpirySeconds { get; set; } = 900;
    public int MinimumExpirySeconds { get; set; } = 300;
    public int MaximumExpirySeconds { get; set; } = 1800;
}

public sealed class CreditPaymentsApplicationException : Exception
{
    public CreditPaymentsApplicationException(string code, string message, int statusCode = 400)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }
    public int StatusCode { get; }
    public bool IsConflict => StatusCode == 409;
}

internal static class PaymentCanonicalization
{
    public static string NormalizeCurrency(string value) => value?.Trim().ToUpperInvariant() ?? string.Empty;

    public static string? NormalizeDescription(string? value)
    {
        var result = value?.Trim();
        return string.IsNullOrEmpty(result) ? null : result;
    }

    public static string CanonicalizeMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        metadata ??= new Dictionary<string, string>();
        if (metadata.Count > 20) throw InvalidMetadata();

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            foreach (var pair in metadata.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                if (pair.Key is null || pair.Key.Length is < 1 or > 40 || pair.Value is null || pair.Value.Length > 200)
                    throw InvalidMetadata();
                writer.WriteString(pair.Key, pair.Value);
            }
            writer.WriteEndObject();
        }
        if (buffer.WrittenCount > 4096) throw InvalidMetadata();
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    public static byte[] Hash(
        Guid merchantId,
        Guid terminalId,
        string merchantOrderId,
        long amountMinor,
        string currency,
        string? description,
        string canonicalReturnUrl,
        int expirySeconds,
        string metadataJson)
    {
        var fields = new[]
        {
            "cpg-create-payment-v1",
            merchantId.ToString("D"), terminalId.ToString("D"), merchantOrderId,
            amountMinor.ToString(CultureInfo.InvariantCulture), currency, description ?? string.Empty,
            canonicalReturnUrl, expirySeconds.ToString(CultureInfo.InvariantCulture), metadataJson
        };
        var canonical = string.Concat(fields.Select(x => $"{Encoding.UTF8.GetByteCount(x)}:{x}"));
        return SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
    }

    public static IReadOnlyDictionary<string, string> DeserializeMetadata(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();

    private static CreditPaymentsApplicationException InvalidMetadata() =>
        new("invalid_metadata", "فراداده نامعتبر است");
}

public sealed class CreateGatewayPaymentHandler(
    ICreditPaymentsRepository repository,
    IGatewayPaymentRepository paymentRepository,
    ICheckoutRepository checkoutRepository,
    ICheckoutSecurityService checkoutSecurity,
    IOptions<GatewayPaymentOptions> options,
    IOptions<CheckoutOptions> checkoutOptions,
    IOptions<RiskOptions> riskOptions,
    TimeProvider timeProvider) : IRequestHandler<CreateGatewayPaymentCommand, CreateGatewayPaymentResponse>
{
    public CreateGatewayPaymentHandler(ICreditPaymentsRepository repository,
        IGatewayPaymentRepository paymentRepository,ICheckoutRepository checkoutRepository,
        ICheckoutSecurityService checkoutSecurity,IOptions<GatewayPaymentOptions> options,
        IOptions<CheckoutOptions> checkoutOptions,TimeProvider timeProvider)
        :this(repository,paymentRepository,checkoutRepository,checkoutSecurity,options,checkoutOptions,
            Microsoft.Extensions.Options.Options.Create(new RiskOptions()),timeProvider) { }

    public async Task<CreateGatewayPaymentResponse> Handle(CreateGatewayPaymentCommand request, CancellationToken ct)
    {
        try { IdempotencyRecord.ValidateKey(request.IdempotencyKey); }
        catch (CreditPaymentsDomainException) { throw Error("idempotency_key_invalid", "کلید تکرارپذیری نامعتبر است"); }
        var merchantOrderId = request.MerchantOrderId?.Trim() ?? string.Empty;
        if (merchantOrderId.Length is < 1 or > 100)
            throw Error("merchant_order_invalid", "شناسه سفارش پذیرنده نامعتبر است");

        var merchant = await repository.GetMerchantAsync(request.MerchantId, false, ct);
        if (merchant is null || merchant.Status != MerchantStatus.Active)
            throw Error("merchant_not_active", "پذیرنده فعال نیست");
        var terminal = await repository.GetTerminalAsync(request.TerminalId, false, ct);
        if (terminal is null || terminal.MerchantId != merchant.Id || terminal.Status != TerminalStatus.Active)
            throw Error("terminal_not_active", "پایانه فعال و متعلق به پذیرنده نیست");
        var currency = PaymentCanonicalization.NormalizeCurrency(request.Currency);
        var description = PaymentCanonicalization.NormalizeDescription(request.Description);
        if (description?.Length > 250) throw Error("description_too_long", "توضیحات حداکثر ۲۵۰ نویسه است");

        string returnUrl;
        try { returnUrl = SecureUrlPolicy.CanonicalizeReturnUrl(request.ReturnUrl); }
        catch (CreditPaymentsDomainException) { throw Error("return_url_not_allowed", "نشانی بازگشت مجاز نیست"); }
        if (!terminal.ReturnUrls.Any(x => string.Equals(x.CanonicalUrl, returnUrl, StringComparison.Ordinal)))
            throw Error("return_url_not_allowed", "نشانی بازگشت مجاز نیست");

        var configured = options.Value;
        var expirySeconds = request.ExpiresInSeconds ?? configured.DefaultExpirySeconds;
        var metadataJson = PaymentCanonicalization.CanonicalizeMetadata(request.Metadata);
        var hash = PaymentCanonicalization.Hash(merchant.Id, terminal.Id, merchantOrderId, request.AmountMinor,
            currency, description, returnUrl, expirySeconds, metadataJson);
        var existing = await paymentRepository.GetIdempotencyRecordAsync(terminal.Id,
            IdempotencyOperationTypes.CreatePayment, request.IdempotencyKey, ct);
        if (existing is not null)
        {
            if (existing.RequestHashVersion != 1 || !CryptographicOperations.FixedTimeEquals(existing.RequestHash, hash))
                throw Error("idempotency_conflict", "کلید تکرارپذیری برای درخواست دیگری استفاده شده است", true);
            if (existing.State != IdempotencyState.Completed || existing.ResourceId is null)
                throw new InvalidOperationException("An idempotency owner exists without a completed resource binding.");
            var existingPayment = await paymentRepository.GetGatewayPaymentAsync(existing.ResourceId.Value, merchant.Id, terminal.Id, ct)
                ?? throw new InvalidOperationException("A completed idempotency record has no payment resource.");
            var existingCheckout = await checkoutRepository.GetCheckoutForPaymentAsync(existingPayment.Id, ct)
                ?? throw new InvalidOperationException("A public payment has no checkout resource.");
            return ToCreateDto(existingPayment, existingCheckout);
        }
        if (request.AmountMinor < terminal.MinPaymentAmountMinor || request.AmountMinor > terminal.MaxPaymentAmountMinor)
            throw Error("amount_out_of_range", "مبلغ خارج از محدوده مجاز پایانه است");
        if (currency != "IRR") throw Error("unsupported_currency", "ارز پشتیبانی نمی‌شود");
        if (expirySeconds < configured.MinimumExpirySeconds || expirySeconds > configured.MaximumExpirySeconds)
            throw Error("expiry_out_of_range", "مدت اعتبار پرداخت خارج از محدوده مجاز است");
        var now = timeProvider.GetUtcNow();
        var payment = GatewayPayment.Create(merchant.Id, terminal.Id, merchantOrderId, request.AmountMinor,
            currency, description, metadataJson, returnUrl, now.AddSeconds(expirySeconds), merchant.DisplayName,
            terminal.Name, terminal.ApprovedCategoryCode, now);
        var checkoutId = Guid.NewGuid();
        var token = checkoutSecurity.CreateCheckoutToken(checkoutId, payment.Id);
        var checkout = CheckoutSession.Create(checkoutId, payment.Id, token.Nonce, token.TokenHash,
            token.KeyVersion, payment.ExpiresAt, now);
        payment.ActivateCheckout(now);
        var idempotency = IdempotencyRecord.Claim(terminal.Id, IdempotencyOperationTypes.CreatePayment,
            request.IdempotencyKey, hash, now);
        var actor = request.CredentialId?.ToString("D");
        var audits = new[]
        {
            AuditRecord.CreateGatewayPayment(payment.Id, merchant.Id, terminal.Id, merchantOrderId, actor, now),
            AuditRecord.Create("checkout-session.created", "CheckoutSession", checkout.Id, actor, now)
        };
        var risk=RiskPolicies.TerminalPayment(riskOptions.Value,payment.Id,merchant.Id,terminal.Id,
            payment.AmountMinor,now);
        PaymentCreationPersistenceResult result;
        try { result = await paymentRepository.CreateGatewayPaymentAsync(payment, checkout, idempotency, audits, risk, ct); }
        catch(Exception ex) when(risk is not null && ex is not CreditPaymentsApplicationException)
        { throw Error("risk_temporarily_unavailable", "سامانه کنترل ریسک موقتاً در دسترس نیست", false, 503); }
        return result.Outcome switch
        {
            PaymentCreationPersistenceOutcome.Created or PaymentCreationPersistenceOutcome.Existing
                => ToCreateDto(result.Payment!, result.CheckoutSession!),
            PaymentCreationPersistenceOutcome.IdempotencyConflict
                => throw Error("idempotency_conflict", "کلید تکرارپذیری برای درخواست دیگری استفاده شده است", true),
            PaymentCreationPersistenceOutcome.MerchantOrderConflict
                => throw Error("merchant_order_already_exists", "برای این شناسه سفارش قبلاً پرداختی ایجاد شده است", true),
            PaymentCreationPersistenceOutcome.RiskDenied
                => throw Error("payment_limit_exceeded", "محدودیت تعداد یا مبلغ پرداخت پایانه عبور کرده است", false, 429),
            _ => throw new InvalidOperationException("Unknown payment persistence outcome.")
        };
    }

    internal CreateGatewayPaymentResponse ToCreateDto(GatewayPayment payment, CheckoutSession checkout)
    {
        var token = checkoutSecurity.RecreateCheckoutToken(checkout);
        var configured = checkoutOptions.Value.PublicBaseUrl?.Trim().TrimEnd('/');
        if (!Uri.TryCreate(configured, UriKind.Absolute, out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("CreditPayments Checkout PublicBaseUrl must be an absolute HTTPS URL.");
        return new(payment.Id, payment.MerchantOrderId, PaymentStatusName(payment.Status), payment.AmountMinor,
            payment.Currency, $"{baseUri.AbsoluteUri.TrimEnd('/')}/c/{token}", payment.ExpiresAt, payment.CreatedAt);
    }

    internal static GatewayPaymentInquiryDto ToInquiryDto(GatewayPayment payment) => new(
        payment.Id, payment.MerchantOrderId, PaymentStatusName(payment.Status), payment.AmountMinor,
        payment.Currency, payment.Description, payment.ExpiresAt, payment.CreatedAt,
        PaymentCanonicalization.DeserializeMetadata(payment.MetadataJson),
        payment.CapturedAmountMinor,
        payment.RefundedAmountMinor,
        payment.PaidAt);

    internal static string PaymentStatusName(GatewayPaymentStatus status) => status switch
    {
        GatewayPaymentStatus.AwaitingCustomer => "awaiting_customer",
        GatewayPaymentStatus.AwaitingFunding => "awaiting_funding",
        GatewayPaymentStatus.PartiallyRefunded => "partially_refunded",
        _ => status.ToString().ToLowerInvariant()
    };

    private static CreditPaymentsApplicationException Error(string code, string message, bool conflict = false, int? status = null) =>
        new(code, message, status ?? (conflict ? 409 : 400));
}

public sealed class GetGatewayPaymentHandler(IGatewayPaymentRepository repository)
    : IRequestHandler<GetGatewayPaymentQuery, GatewayPaymentInquiryDto?>
{
    public async Task<GatewayPaymentInquiryDto?> Handle(GetGatewayPaymentQuery request, CancellationToken ct)
    {
        var payment = await repository.GetGatewayPaymentAsync(request.PaymentId, request.MerchantId, request.TerminalId, ct);
        return payment is null ? null : CreateGatewayPaymentHandler.ToInquiryDto(payment);
    }
}
