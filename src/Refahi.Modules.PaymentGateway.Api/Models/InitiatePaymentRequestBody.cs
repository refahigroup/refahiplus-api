using Refahi.Modules.PaymentGateway.Domain.Enums;

namespace Refahi.Modules.PaymentGateway.Api.Models;

public sealed record InitiatePaymentRequestBody(
    Guid WalletId,
    long AmountMinor,
    PaymentGatewayProviderType Provider,
    /// <summary>
    /// آدرس پایه صفحه نتیجه در Blazor.
    /// Backend شناسه جلسه را به انتها اضافه می‌کند: {ReturnBaseUrl}/{sessionId}
    /// مثال: "https://app.refahi.ir/charge/wallet/topup/result"
    /// </summary>
    string ReturnBaseUrl,
    string? SucceededCallbackUrl = null,
    string? FailedCallbackUrl = null
);
