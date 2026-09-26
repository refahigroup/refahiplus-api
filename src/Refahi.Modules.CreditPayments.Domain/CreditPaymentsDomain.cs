using System.Net;
using System.Text.RegularExpressions;

namespace Refahi.Modules.CreditPayments.Domain;

public sealed class CreditPaymentsDomainException(string message) : Exception(message);

public enum MerchantStatus : short { Pending = 1, Active = 2, Suspended = 3, Disabled = 4 }
public enum MerchantEntityType : short { Individual = 1, Legal = 2 }
public enum TerminalStatus : short { Active = 1, Suspended = 2, Disabled = 3 }
public enum CredentialStatus : short { Active = 1, Revoked = 2 }
public enum WebhookEndpointStatus : short { Active = 1, Disabled = 2 }

public static class CredentialScopes
{
    public const string PaymentCreate = "payment:create";
    public const string PaymentRead = "payment:read";
    public const string PaymentCancel = "payment:cancel";
    public const string RefundCreate = "refund:create";
    public const string RefundRead = "refund:read";
    public const string WebhookRead = "webhook:read";
    public const string WebhookManage = "webhook:manage";
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    { PaymentCreate, PaymentRead, PaymentCancel, RefundCreate, RefundRead, WebhookRead, WebhookManage };

    public static string Normalize(IEnumerable<string> scopes)
    {
        var normalized = scopes.Select(x => x.Trim().ToLowerInvariant()).Distinct().Order().ToArray();
        if (normalized.Length == 0 || normalized.Any(x => !All.Contains(x)))
            throw new CreditPaymentsDomainException("سطوح دسترسی نامعتبر است");
        return string.Join(' ', normalized);
    }
}

public static partial class CreditPaymentsText
{
    [GeneratedRegex("^[a-z0-9]+(?:[.-][a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex CategoryRegex();
    public static string Required(string value, int max, string message)
    {
        var result = value?.Trim() ?? string.Empty;
        if (result.Length == 0 || result.Length > max) throw new CreditPaymentsDomainException(message);
        return result;
    }
    public static string Category(string value)
    {
        var result = Required(value, 100, "کد دسته‌بندی الزامی است").ToLowerInvariant();
        if (!CategoryRegex().IsMatch(result)) throw new CreditPaymentsDomainException("کد دسته‌بندی نامعتبر است");
        return result;
    }
}

public static class SecureUrlPolicy
{
    public static string CanonicalizeReturnUrl(string value) => Canonicalize(value, false);
    public static string CanonicalizeWebhookUrl(string value) => Canonicalize(value, true);

    private static string Canonicalize(string value, bool rejectPrivate)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment))
            throw new CreditPaymentsDomainException("نشانی باید HTTPS مطلق، بدون اطلاعات کاربری و fragment باشد");
        var host = uri.IdnHost.ToLowerInvariant();
        if (host == "localhost" || host.EndsWith(".localhost", StringComparison.Ordinal) || IsUnsafeIp(host, rejectPrivate))
            throw new CreditPaymentsDomainException("نشانی محلی یا غیرعمومی مجاز نیست");
        var builder = new UriBuilder(uri) { Scheme = "https", Host = host, Fragment = string.Empty };
        if (builder.Port == 443) builder.Port = -1;
        if (string.IsNullOrEmpty(builder.Path)) builder.Path = "/";
        return builder.Uri.AbsoluteUri;
    }

    private static bool IsUnsafeIp(string host, bool rejectPrivate)
    {
        if (!IPAddress.TryParse(host, out var ip)) return false;
        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any)) return true;
        if (!rejectPrivate) return false;
        var bytes = ip.GetAddressBytes();
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            return bytes[0] is 10 or 127 || bytes[0] == 169 && bytes[1] == 254 || bytes[0] == 172 && bytes[1] is >= 16 and <= 31 || bytes[0] == 192 && bytes[1] == 168;
        return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || bytes[0] == 0xfc || bytes[0] == 0xfd;
    }
}

public sealed class Merchant
{
    private readonly List<Terminal> _terminals = [];
    private Merchant() { }
    public Guid Id { get; private set; }
    public MerchantEntityType EntityType { get; private set; }
    public string NationalId { get; private set; } = string.Empty;
    public string LegalName { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public Guid? SettlementWalletId { get; private set; }
    public MerchantStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }
    public IReadOnlyCollection<Terminal> Terminals => _terminals;

    public static Merchant Create(MerchantEntityType entityType, string nationalId, string legalName, string? displayName, DateTimeOffset now)
    {
        if (!Enum.IsDefined(entityType)) throw new CreditPaymentsDomainException("نوع شخصیت پذیرنده معتبر نیست");
        nationalId = NormalizeNationalId(nationalId);
        if (nationalId.Length != (entityType == MerchantEntityType.Individual ? 10 : 11) || !nationalId.All(char.IsAsciiDigit))
            throw new CreditPaymentsDomainException("کد ملی یا شناسه ملی معتبر نیست");
        legalName = CreditPaymentsText.Required(legalName, 200, "نام قانونی پذیرنده الزامی است");
        var name = string.IsNullOrWhiteSpace(displayName) ? legalName : CreditPaymentsText.Required(displayName, 200, "نام نمایشی نامعتبر است");
        return new Merchant { Id = Guid.NewGuid(), EntityType = entityType, NationalId = nationalId, LegalName = legalName, DisplayName = name, Status = MerchantStatus.Pending, CreatedAt = now, UpdatedAt = now, Version = 1 };
    }
    public static string NormalizeNationalId(string? value) => new((value ?? string.Empty).Trim().Select(c => c switch { >= '\u06f0' and <= '\u06f9' => (char)('0' + c - '\u06f0'), >= '\u0660' and <= '\u0669' => (char)('0' + c - '\u0660'), _ => c }).ToArray());
    public bool HasSameIdentity(MerchantEntityType type, string nationalId, string legalName, string? displayName) =>
        EntityType == type && NationalId == NormalizeNationalId(nationalId) && LegalName == (legalName?.Trim() ?? string.Empty) &&
        DisplayName == (string.IsNullOrWhiteSpace(displayName) ? legalName.Trim() : displayName.Trim());
    public void BindSettlementWallet(Guid walletId, DateTimeOffset now) { if (walletId == Guid.Empty || (SettlementWalletId.HasValue && SettlementWalletId != walletId)) throw new CreditPaymentsDomainException("کیف درآمد پذیرنده معتبر نیست"); if (SettlementWalletId == walletId) return; SettlementWalletId = walletId; Touch(now); }
    public void Activate(DateTimeOffset now) { if (Status is not (MerchantStatus.Pending or MerchantStatus.Suspended)) Invalid(); if (!SettlementWalletId.HasValue) throw new CreditPaymentsDomainException("کیف درآمد پذیرنده ثبت نشده است"); Status = MerchantStatus.Active; Touch(now); }
    public void Suspend(DateTimeOffset now) { if (Status != MerchantStatus.Active) Invalid(); Status = MerchantStatus.Suspended; Touch(now); }
    public void Disable(DateTimeOffset now) { if (Status == MerchantStatus.Disabled) Invalid(); Status = MerchantStatus.Disabled; Touch(now); }
    public Terminal AddTerminal(string name, string category, long min, long max, IEnumerable<string> urls, DateTimeOffset now)
    {
        if (Status != MerchantStatus.Active) throw new CreditPaymentsDomainException("پذیرنده فعال نیست");
        if (_terminals.Any(x => x.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))) throw new CreditPaymentsDomainException("نام پایانه تکراری است");
        var terminal = Terminal.Create(Id, name, category, min, max, urls, now); _terminals.Add(terminal); Touch(now); return terminal;
    }
    private void Touch(DateTimeOffset now) { UpdatedAt = now; Version++; }
    private static void Invalid() => throw new CreditPaymentsDomainException("تغییر وضعیت پذیرنده مجاز نیست");
}

public sealed class Terminal
{
    private readonly List<TerminalReturnUrl> _returnUrls = [];
    private readonly List<TerminalCredential> _credentials = [];
    private Terminal() { }
    public Guid Id { get; private set; }
    public Guid MerchantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public TerminalStatus Status { get; private set; }
    public string ApprovedCategoryCode { get; private set; } = string.Empty;
    public long MinPaymentAmountMinor { get; private set; }
    public long MaxPaymentAmountMinor { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }
    public IReadOnlyCollection<TerminalReturnUrl> ReturnUrls => _returnUrls;
    public IReadOnlyCollection<TerminalCredential> Credentials => _credentials;
    public WebhookEndpoint? WebhookEndpoint { get; private set; }
    internal static Terminal Create(Guid merchantId, string name, string category, long min, long max, IEnumerable<string> urls, DateTimeOffset now)
    {
        ValidateLimits(min, max);
        var result = new Terminal { Id = Guid.NewGuid(), MerchantId = merchantId, Name = CreditPaymentsText.Required(name, 150, "نام پایانه الزامی است"), Status = TerminalStatus.Active, ApprovedCategoryCode = CreditPaymentsText.Category(category), MinPaymentAmountMinor = min, MaxPaymentAmountMinor = max, CreatedAt = now, UpdatedAt = now, Version = 1 };
        foreach (var url in urls) result.AddReturnUrl(url, now);
        if (result._returnUrls.Count == 0) throw new CreditPaymentsDomainException("حداقل یک نشانی بازگشت الزامی است");
        return result;
    }
    public void UpdatePolicy(string category, long min, long max, DateTimeOffset now) { if (Status == TerminalStatus.Disabled) throw new CreditPaymentsDomainException("پایانه غیرفعال است"); ValidateLimits(min,max); ApprovedCategoryCode=CreditPaymentsText.Category(category); MinPaymentAmountMinor=min; MaxPaymentAmountMinor=max; Touch(now); }
    public TerminalReturnUrl AddReturnUrl(string url, DateTimeOffset now) { var canonical=SecureUrlPolicy.CanonicalizeReturnUrl(url); if (_returnUrls.Any(x=>x.CanonicalUrl==canonical)) throw new CreditPaymentsDomainException("نشانی بازگشت تکراری است"); var item=TerminalReturnUrl.Create(Id,canonical,now); _returnUrls.Add(item); Touch(now); return item; }
    public void RemoveReturnUrl(Guid id, DateTimeOffset now) { var item=_returnUrls.SingleOrDefault(x=>x.Id==id) ?? throw new CreditPaymentsDomainException("نشانی بازگشت یافت نشد"); if (_returnUrls.Count==1) throw new CreditPaymentsDomainException("حداقل یک نشانی بازگشت الزامی است"); _returnUrls.Remove(item); Touch(now); }
    public TerminalCredential AddCredential(string clientId, string hash, string algorithm, int hashVersion, IEnumerable<string> scopes, string label, DateTimeOffset? expiresAt, DateTimeOffset now) { if(Status!=TerminalStatus.Active) throw new CreditPaymentsDomainException("پایانه فعال نیست"); var item=TerminalCredential.Create(Id,clientId,hash,algorithm,hashVersion,scopes,label,expiresAt,now); _credentials.Add(item); Touch(now); return item; }
    public void Suspend(DateTimeOffset now) { if(Status!=TerminalStatus.Active) Invalid(); Status=TerminalStatus.Suspended; Touch(now); }
    public void Activate(DateTimeOffset now) { if(Status!=TerminalStatus.Suspended) Invalid(); Status=TerminalStatus.Active; Touch(now); }
    public void Disable(DateTimeOffset now) { if(Status==TerminalStatus.Disabled) Invalid(); Status=TerminalStatus.Disabled; Touch(now); }
    public WebhookEndpoint ConfigureWebhook(string url,string protectedSecret,int secretVersion,DateTimeOffset now) { if(Status==TerminalStatus.Disabled) throw new CreditPaymentsDomainException("پایانه غیرفعال است"); if(WebhookEndpoint is null) WebhookEndpoint=WebhookEndpoint.Create(Id,url,protectedSecret,secretVersion,now); else WebhookEndpoint.Rotate(url,protectedSecret,secretVersion,now); Touch(now); return WebhookEndpoint; }
    public void DisableWebhook(DateTimeOffset now) { if(WebhookEndpoint is null) throw new CreditPaymentsDomainException("وب‌هوک پیکربندی نشده است"); WebhookEndpoint.Disable(now); Touch(now); }
    private static void ValidateLimits(long min,long max) { if(min<=0||max<=0||min>max) throw new CreditPaymentsDomainException("محدوده مبلغ نامعتبر است"); }
    private void Touch(DateTimeOffset now) { UpdatedAt=now; Version++; }
    private static void Invalid()=>throw new CreditPaymentsDomainException("تغییر وضعیت پایانه مجاز نیست");
}

public sealed class TerminalReturnUrl
{
    private TerminalReturnUrl() { }
    public Guid Id { get; private set; } public Guid TerminalId { get; private set; } public string CanonicalUrl { get; private set; }=string.Empty; public DateTimeOffset CreatedAt { get; private set; }
    internal static TerminalReturnUrl Create(Guid terminalId,string url,DateTimeOffset now)=>new(){Id=Guid.NewGuid(),TerminalId=terminalId,CanonicalUrl=url,CreatedAt=now};
}

public sealed class TerminalCredential
{
    private TerminalCredential() { }
    public Guid Id { get; private set; } public Guid TerminalId { get; private set; } public string ClientId { get; private set; }=string.Empty; public string SecretHash { get; private set; }=string.Empty; public string SecretHashAlgorithm { get; private set; }=string.Empty; public int SecretHashVersion { get; private set; } public string Scopes { get; private set; }=string.Empty; public CredentialStatus Status { get; private set; } public string Label { get; private set; }=string.Empty; public DateTimeOffset CreatedAt { get; private set; } public DateTimeOffset? ExpiresAt { get; private set; } public DateTimeOffset? LastUsedAt { get; private set; } public DateTimeOffset? RevokedAt { get; private set; }
    public bool IsExpired(DateTimeOffset now)=>ExpiresAt is not null && ExpiresAt<=now;
    internal static TerminalCredential Create(Guid terminalId,string clientId,string hash,string algorithm,int version,IEnumerable<string> scopes,string label,DateTimeOffset? expiresAt,DateTimeOffset now) { if(expiresAt<=now) throw new CreditPaymentsDomainException("تاریخ انقضا نامعتبر است"); return new(){Id=Guid.NewGuid(),TerminalId=terminalId,ClientId=CreditPaymentsText.Required(clientId,100,"شناسه مشتری الزامی است"),SecretHash=CreditPaymentsText.Required(hash,500,"هش راز الزامی است"),SecretHashAlgorithm=CreditPaymentsText.Required(algorithm,30,"الگوریتم هش الزامی است"),SecretHashVersion=version,Scopes=CredentialScopes.Normalize(scopes),Status=CredentialStatus.Active,Label=CreditPaymentsText.Required(label,100,"برچسب اعتبارنامه الزامی است"),CreatedAt=now,ExpiresAt=expiresAt}; }
    public void Revoke(DateTimeOffset now) { if(Status==CredentialStatus.Revoked) throw new CreditPaymentsDomainException("اعتبارنامه قبلاً لغو شده است"); Status=CredentialStatus.Revoked; RevokedAt=now; }
    public void RecordUsed(DateTimeOffset now) { if(LastUsedAt is null || now-LastUsedAt>=TimeSpan.FromMinutes(15)) LastUsedAt=now; }
}

public sealed class WebhookEndpoint
{
    private WebhookEndpoint() { }
    public Guid Id { get; private set; } public Guid TerminalId { get; private set; } public string Url { get; private set; }=string.Empty; public WebhookEndpointStatus Status { get; private set; } public string SigningSecretCiphertext { get; private set; }=string.Empty; public int SigningSecretVersion { get; private set; } public DateTimeOffset CreatedAt { get; private set; } public DateTimeOffset UpdatedAt { get; private set; }
    internal static WebhookEndpoint Create(Guid terminalId,string url,string ciphertext,int version,DateTimeOffset now)=>new(){Id=Guid.NewGuid(),TerminalId=terminalId,Url=SecureUrlPolicy.CanonicalizeWebhookUrl(url),Status=WebhookEndpointStatus.Active,SigningSecretCiphertext=CreditPaymentsText.Required(ciphertext,2000,"راز محافظت‌شده الزامی است"),SigningSecretVersion=version,CreatedAt=now,UpdatedAt=now};
    internal void Rotate(string url,string ciphertext,int version,DateTimeOffset now) { Url=SecureUrlPolicy.CanonicalizeWebhookUrl(url); SigningSecretCiphertext=CreditPaymentsText.Required(ciphertext,2000,"راز محافظت‌شده الزامی است"); SigningSecretVersion=version; Status=WebhookEndpointStatus.Active; UpdatedAt=now; }
    public void Disable(DateTimeOffset now) { Status=WebhookEndpointStatus.Disabled; UpdatedAt=now; }
}

public sealed class AuditRecord
{
    private AuditRecord() { }
    public Guid Id { get; private set; } public string Action { get; private set; }=string.Empty; public string EntityType { get; private set; }=string.Empty; public Guid EntityId { get; private set; } public string? ActorId { get; private set; } public Guid? MerchantId { get; private set; } public Guid? TerminalId { get; private set; } public string? MerchantOrderId { get; private set; } public DateTimeOffset OccurredAt { get; private set; }
    public static AuditRecord Create(string action,string entityType,Guid entityId,string? actor,DateTimeOffset now)=>new(){Id=Guid.NewGuid(),Action=CreditPaymentsText.Required(action,100,"عملیات حسابرسی الزامی است"),EntityType=CreditPaymentsText.Required(entityType,50,"نوع موجودیت الزامی است"),EntityId=entityId,ActorId=actor?.Trim(),OccurredAt=now};
    public static AuditRecord CreateGatewayPayment(Guid paymentId,Guid merchantId,Guid terminalId,string merchantOrderId,string? actor,DateTimeOffset now)=>new(){Id=Guid.NewGuid(),Action="gateway-payment.created",EntityType="GatewayPayment",EntityId=paymentId,ActorId=actor?.Trim(),MerchantId=merchantId,TerminalId=terminalId,MerchantOrderId=CreditPaymentsText.Required(merchantOrderId,100,"شناسه سفارش پذیرنده نامعتبر است"),OccurredAt=now};
}
