using Refahi.Modules.Charge.Domain.Enums;

namespace Refahi.Modules.Charge.Application.Contracts.Providers;

public interface IChargeProvider
{
    string Name { get; }
    Task<IReadOnlyList<ChargeProductDto>> GetProductsAsync(ChargeOperator @operator, CancellationToken ct);
    Task<IReadOnlyList<ChargeProductDto>> GetOffersAsync(ChargeOperator @operator, string mobileNumber, ChargeOfferCategory category, CancellationToken ct);
    Task<ChargeEligibilityDto> CheckEligibilityAsync(ChargeEligibilityRequest request, CancellationToken ct);
    Task<ChargePostpaidBalanceDto> GetPostpaidBalanceAsync(ChargeOperator @operator, string mobileNumber, CancellationToken ct);
    Task<IReadOnlyList<PinChargeCategoryDto>> GetPinCategoriesAsync(CancellationToken ct);
    Task<IReadOnlyList<PackageTypeDto>> GetPackageTypesAsync(CancellationToken ct);
    Task<IReadOnlyList<ProviderChannelDto>> GetChannelsAsync(CancellationToken ct);
    Task<ProviderPurchaseResultDto> PurchaseAsync(ProviderPurchaseRequest request, CancellationToken ct);
    Task<ProviderTraceResultDto> TraceAsync(ProviderTraceRequest request, CancellationToken ct);
    Task<ProviderBalanceDto> GetBalanceAsync(CancellationToken ct);
    Task<IReadOnlyList<ProviderErrorDto>> GetErrorsAsync(CancellationToken ct);
    Task<ProviderReportDto> GetTransactionReportAsync(ProviderReportRequest request, CancellationToken ct);
    Task<ProviderReportDto> GetWalletChargeReportAsync(ProviderReportRequest request, CancellationToken ct);
}

public interface IChargeProviderResolver
{
    IChargeProvider Get(string providerName);
    IChargeProvider GetDefault();
}

public interface IChargeSecretProtector
{
    string Protect(string value);
    string Unprotect(string value);
}

public enum ChargeOfferCategory : short
{
    All = 0, Hourly = 1, Daily = 2, Weekly = 3, Monthly = 4,
    Voice = 10, Internet = 11, Sms = 12
}

public sealed record ChargeProductDto(
    string ProviderProductId, string Title, string? Detail, int PackageTypeCode,
    string? PackageTypeName, ChargeOperator Operator, long AmountMinor, long AmountWithTaxMinor,
    string? Capacity, int DurationDays, bool IsActive, int? ProductCategory, string? ProductCategoryName,
    bool IsPersonalized = false);

public sealed record ChargeEligibilityRequest(
    ChargeOperator Operator, string DestinationMobileNumber, long AmountMinor,
    string ProviderProductId, int ProductCategory);

public sealed record ChargeEligibilityDto(bool Supported, bool? Eligible, string? LineType, int EniacResultCode, string? OperatorResultCode, string? Message);
public sealed record ChargePostpaidBalanceDto(string CorrelationId, long CurrentBalanceMinor, long? OutstandingBalanceMinor, int ResultCode, string? ResultMessage);
public sealed record PinChargeCategoryDto(
    int CategoryId, string Code, long AmountMinor, string Description, ChargeOperator Operator);
public sealed record PackageTypeDto(string Title, string? PersianTitle, int PackageTypeCode, ChargeOperator Operator, string OperatorName);
public sealed record ProviderChannelDto(int ChannelId, string ChannelName);

public sealed record ProviderPurchaseRequest(
    ChargeOperator Operator, ChargeServiceType ServiceType, string? OriginMobileNumber,
    string DestinationMobileNumber, long AmountMinor, string CustomerInvoiceNumber,
    string ProviderProductId, int ProductCategory, int PayBill, int ChannelId,
    string? ResellerName, int? PinCategoryId, int PinCount);

public sealed record ProviderPinDto(string Serial, string Code, long AmountMinor);
public sealed record ProviderPurchaseResultDto(
    bool Success, int EniacResultCode, string? OperatorResultCode, string? Message,
    string? Rrn, string? CustomerInvoiceNumber, string? OperatorTraceId,
    IReadOnlyList<ProviderPinDto> Pins, string RequestSnapshotJson, string ResponseSnapshotJson, long LatencyMilliseconds);

public sealed record ProviderTraceRequest(long EniacRrn, string CustomerInvoiceNumber, string ProviderProductId, long AmountMinor, DateOnly Date);
public sealed record ProviderTraceResultDto(
    bool Success, int EniacResultCode, string? OperatorResultCode, string? Message,
    string? Rrn, string? OperatorTraceId, int? PaymentResultCode, int? ReverseResultCode,
    IReadOnlyList<ProviderPinDto> Pins, string RequestSnapshotJson, string ResponseSnapshotJson, long LatencyMilliseconds);

public sealed record ProviderBalanceDto(long TotalBalanceMinor, long SuspendedBalanceMinor, long AvailableBalanceMinor, bool IsConfidential);
public sealed record ProviderErrorDto(int Code, string Message);
public sealed record ProviderReportRequest(int PageNumber, DateOnly? FromDate, DateOnly? ToDate);
public sealed record ProviderReportDto(string DataJson, int TotalCount, bool Success, int EniacResultCode, string? Message);
