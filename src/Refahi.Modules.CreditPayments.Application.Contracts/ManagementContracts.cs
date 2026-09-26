using MediatR;

namespace Refahi.Modules.CreditPayments.Application.Contracts;

public sealed record MerchantDto(Guid Id, short EntityType, string NationalId, string LegalName, string DisplayName, Guid? SettlementWalletId, short Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, long Version);
public sealed record ReturnUrlDto(Guid Id, string Url, DateTimeOffset CreatedAt);
public sealed record CredentialDto(Guid Id, string ClientId, string Label, string[] Scopes, short Status, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, DateTimeOffset? LastUsedAt, DateTimeOffset? RevokedAt);
public sealed record WebhookDto(Guid Id, string Url, short Status, int SecretVersion, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record TerminalDto(Guid Id, Guid MerchantId, string Name, short Status, string ApprovedCategoryCode, long MinPaymentAmountMinor, long MaxPaymentAmountMinor, IReadOnlyList<ReturnUrlDto> ReturnUrls, WebhookDto? Webhook, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, long Version);
public sealed record IssuedCredentialDto(Guid CredentialId, string ClientId, string ClientSecret, string[] Scopes, DateTimeOffset? ExpiresAt, DateTimeOffset CreatedAt);
public sealed record ConfiguredWebhookDto(WebhookDto Webhook, string SigningSecret);

public sealed record CreateMerchantCommand(short EntityType, string NationalId, string LegalName, string? DisplayName, string? ActorId=null) : IRequest<MerchantDto>;
public sealed record GetMerchantQuery(Guid MerchantId) : IRequest<MerchantDto?>;
public sealed record ChangeMerchantStatusCommand(Guid MerchantId, string Action, string? ActorId=null) : IRequest<MerchantDto>;
public sealed record CreateTerminalCommand(Guid MerchantId, string Name, string ApprovedCategoryCode, long MinPaymentAmountMinor, long MaxPaymentAmountMinor, IReadOnlyList<string> AllowedReturnUrls, string? ActorId=null) : IRequest<TerminalDto>;
public sealed record GetTerminalQuery(Guid TerminalId) : IRequest<TerminalDto?>;
public sealed record UpdateTerminalPolicyCommand(Guid TerminalId, string ApprovedCategoryCode, long MinPaymentAmountMinor, long MaxPaymentAmountMinor, long ExpectedVersion, string? ActorId=null) : IRequest<TerminalDto>;
public sealed record AddReturnUrlCommand(Guid TerminalId, string Url, long ExpectedVersion, string? ActorId=null) : IRequest<TerminalDto>;
public sealed record RemoveReturnUrlCommand(Guid TerminalId, Guid ReturnUrlId, long ExpectedVersion, string? ActorId=null) : IRequest<TerminalDto>;
public sealed record ChangeTerminalStatusCommand(Guid TerminalId, string Action, long ExpectedVersion, string? ActorId=null) : IRequest<TerminalDto>;
public sealed record IssueCredentialCommand(Guid TerminalId, string Label, IReadOnlyList<string> Scopes, DateTimeOffset? ExpiresAt, string? ActorId=null) : IRequest<IssuedCredentialDto>;
public sealed record RevokeCredentialCommand(Guid CredentialId, string? ActorId=null) : IRequest<Unit>;
public sealed record ConfigureWebhookCommand(Guid TerminalId, string Url, long ExpectedVersion, string? ActorId=null) : IRequest<ConfiguredWebhookDto>;
public sealed record DisableWebhookCommand(Guid TerminalId, long ExpectedVersion, string? ActorId=null) : IRequest<TerminalDto>;
