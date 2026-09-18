using MediatR;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.Suppliers;

namespace Refahi.Modules.CreditPayments.Application;

internal static class Maps
{
    public static MerchantDto ToDto(this Merchant x)=>new(x.Id,x.SupplierId,x.DisplayName,(short)x.Status,x.CreatedAt,x.UpdatedAt,x.Version);
    public static TerminalDto ToDto(this Terminal x)=>new(x.Id,x.MerchantId,x.Name,(short)x.Status,x.ApprovedCategoryCode,x.MinPaymentAmountMinor,x.MaxPaymentAmountMinor,x.ReturnUrls.Select(r=>new ReturnUrlDto(r.Id,r.CanonicalUrl,r.CreatedAt)).ToArray(),x.WebhookEndpoint is null?null:new WebhookDto(x.WebhookEndpoint.Id,x.WebhookEndpoint.Url,(short)x.WebhookEndpoint.Status,x.WebhookEndpoint.SigningSecretVersion,x.WebhookEndpoint.CreatedAt,x.WebhookEndpoint.UpdatedAt),x.CreatedAt,x.UpdatedAt,x.Version);
    public static string[] ScopeArray(this string value)=>value.Split(' ',StringSplitOptions.RemoveEmptyEntries);
}

public sealed class CreateMerchantHandler(ICreditPaymentsRepository repository, IMediator mediator) : IRequestHandler<CreateMerchantCommand,MerchantDto>
{
    public async Task<MerchantDto> Handle(CreateMerchantCommand request,CancellationToken ct)
    {
        if(await repository.GetMerchantBySupplierAsync(request.SupplierId,ct) is not null) throw new CreditPaymentsDomainException("برای این تامین‌کننده قبلاً پذیرنده ایجاد شده است");
        var supplier=await mediator.Send(new GetSupplierProvisioningEligibilityQuery(request.SupplierId),ct) ?? throw new CreditPaymentsDomainException("تامین‌کننده یافت نشد");
        if(!supplier.IsApproved||supplier.IsTerminalLifecycleState) throw new CreditPaymentsDomainException("تامین‌کننده تاییدشده و واجد شرایط نیست");
        var display=request.DisplayName?.Trim();
        if(string.IsNullOrEmpty(display)) display=supplier.DisplayName;
        var now=DateTimeOffset.UtcNow; var merchant=Merchant.Create(request.SupplierId,display!,now); repository.Add(merchant); repository.AddAudit(AuditRecord.Create("merchant.created","Merchant",merchant.Id,request.ActorId,now)); await repository.SaveChangesAsync(ct); return merchant.ToDto();
    }
}
public sealed class GetMerchantHandler(ICreditPaymentsRepository repository):IRequestHandler<GetMerchantQuery,MerchantDto?> { public async Task<MerchantDto?> Handle(GetMerchantQuery r,CancellationToken ct)=>(await repository.GetMerchantAsync(r.MerchantId,false,ct))?.ToDto(); }
public sealed class ChangeMerchantStatusHandler(ICreditPaymentsRepository repository,IMediator mediator):IRequestHandler<ChangeMerchantStatusCommand,MerchantDto>
{
    public async Task<MerchantDto> Handle(ChangeMerchantStatusCommand r,CancellationToken ct){var x=await repository.GetMerchantAsync(r.MerchantId,true,ct)??throw new CreditPaymentsDomainException("پذیرنده یافت نشد");var action=r.Action.ToLowerInvariant();if(action=="activate"){var supplier=await mediator.Send(new GetSupplierProvisioningEligibilityQuery(x.SupplierId),ct);if(supplier is null||!supplier.IsApproved||supplier.IsTerminalLifecycleState)throw new CreditPaymentsDomainException("تامین‌کننده تاییدشده و واجد شرایط نیست");}var now=DateTimeOffset.UtcNow;switch(action){case "activate":x.Activate(now);break;case "suspend":x.Suspend(now);break;case "disable":x.Disable(now);break;default:throw new CreditPaymentsDomainException("عملیات وضعیت نامعتبر است");}repository.AddAudit(AuditRecord.Create($"merchant.{action}","Merchant",x.Id,r.ActorId,now));await repository.SaveChangesAsync(ct);return x.ToDto();}
}
public sealed class CreateTerminalHandler(ICreditPaymentsRepository repository):IRequestHandler<CreateTerminalCommand,TerminalDto>
{
    public async Task<TerminalDto> Handle(CreateTerminalCommand r,CancellationToken ct){var m=await repository.GetMerchantAsync(r.MerchantId,true,ct)??throw new CreditPaymentsDomainException("پذیرنده یافت نشد");var now=DateTimeOffset.UtcNow;var x=m.AddTerminal(r.Name,r.ApprovedCategoryCode,r.MinPaymentAmountMinor,r.MaxPaymentAmountMinor,r.AllowedReturnUrls,now);repository.AddAudit(AuditRecord.Create("terminal.created","Terminal",x.Id,r.ActorId,now));await repository.SaveChangesAsync(ct);return x.ToDto();}
}
public sealed class GetTerminalHandler(ICreditPaymentsRepository repository):IRequestHandler<GetTerminalQuery,TerminalDto?> { public async Task<TerminalDto?> Handle(GetTerminalQuery r,CancellationToken ct)=>(await repository.GetTerminalAsync(r.TerminalId,false,ct))?.ToDto(); }
public sealed class UpdateTerminalPolicyHandler(ICreditPaymentsRepository repository):IRequestHandler<UpdateTerminalPolicyCommand,TerminalDto>
{
    public async Task<TerminalDto> Handle(UpdateTerminalPolicyCommand r,CancellationToken ct){var x=await repository.GetTerminalAsync(r.TerminalId,true,ct)??throw new CreditPaymentsDomainException("پایانه یافت نشد");if(x.Version!=r.ExpectedVersion)throw new CreditPaymentsDomainException("نسخه پایانه تغییر کرده است");var now=DateTimeOffset.UtcNow;x.UpdatePolicy(r.ApprovedCategoryCode,r.MinPaymentAmountMinor,r.MaxPaymentAmountMinor,now);repository.AddAudit(AuditRecord.Create("terminal.policy.updated","Terminal",x.Id,r.ActorId,now));await repository.SaveChangesAsync(ct);return x.ToDto();}
}
public sealed class ReturnUrlHandlers(ICreditPaymentsRepository repository):IRequestHandler<AddReturnUrlCommand,TerminalDto>,IRequestHandler<RemoveReturnUrlCommand,TerminalDto>
{
    public async Task<TerminalDto> Handle(AddReturnUrlCommand r,CancellationToken ct){var x=await Load(r.TerminalId,r.ExpectedVersion,ct);var now=DateTimeOffset.UtcNow;x.AddReturnUrl(r.Url,now);repository.AddAudit(AuditRecord.Create("terminal.return-url.added","Terminal",x.Id,r.ActorId,now));await repository.SaveChangesAsync(ct);return x.ToDto();}
    public async Task<TerminalDto> Handle(RemoveReturnUrlCommand r,CancellationToken ct){var x=await Load(r.TerminalId,r.ExpectedVersion,ct);var now=DateTimeOffset.UtcNow;x.RemoveReturnUrl(r.ReturnUrlId,now);repository.AddAudit(AuditRecord.Create("terminal.return-url.removed","Terminal",x.Id,r.ActorId,now));await repository.SaveChangesAsync(ct);return x.ToDto();}
    private async Task<Terminal> Load(Guid id,long version,CancellationToken ct){var x=await repository.GetTerminalAsync(id,true,ct)??throw new CreditPaymentsDomainException("پایانه یافت نشد");if(x.Version!=version)throw new CreditPaymentsDomainException("نسخه پایانه تغییر کرده است");return x;}
}
public sealed class ChangeTerminalStatusHandler(ICreditPaymentsRepository repository):IRequestHandler<ChangeTerminalStatusCommand,TerminalDto>
{
    public async Task<TerminalDto> Handle(ChangeTerminalStatusCommand r,CancellationToken ct){var x=await repository.GetTerminalAsync(r.TerminalId,true,ct)??throw new CreditPaymentsDomainException("پایانه یافت نشد");if(x.Version!=r.ExpectedVersion)throw new CreditPaymentsDomainException("نسخه پایانه تغییر کرده است");var now=DateTimeOffset.UtcNow;switch(r.Action.ToLowerInvariant()){case "activate":x.Activate(now);break;case "suspend":x.Suspend(now);break;case "disable":x.Disable(now);break;default:throw new CreditPaymentsDomainException("عملیات وضعیت نامعتبر است");}repository.AddAudit(AuditRecord.Create($"terminal.{r.Action.ToLowerInvariant()}","Terminal",x.Id,r.ActorId,now));await repository.SaveChangesAsync(ct);return x.ToDto();}
}
public sealed class IssueCredentialHandler(ICreditPaymentsRepository repository,ICredentialSecretService secrets):IRequestHandler<IssueCredentialCommand,IssuedCredentialDto>
{
    public async Task<IssuedCredentialDto> Handle(IssueCredentialCommand r,CancellationToken ct){var x=await repository.GetTerminalAsync(r.TerminalId,true,ct)??throw new CreditPaymentsDomainException("پایانه یافت نشد");var material=secrets.Generate();var now=DateTimeOffset.UtcNow;var c=x.AddCredential(material.ClientId,material.Hash,material.Algorithm,material.Version,r.Scopes,r.Label,r.ExpiresAt,now);repository.AddAudit(AuditRecord.Create("credential.issued","TerminalCredential",c.Id,r.ActorId,now));await repository.SaveChangesAsync(ct);return new(c.Id,c.ClientId,material.Secret,c.Scopes.ScopeArray(),c.ExpiresAt,c.CreatedAt);}
}
public sealed class RevokeCredentialHandler(ICreditPaymentsRepository repository):IRequestHandler<RevokeCredentialCommand,Unit>
{
    public async Task<Unit> Handle(RevokeCredentialCommand r,CancellationToken ct){var x=await repository.GetCredentialAsync(r.CredentialId,true,ct)??throw new CreditPaymentsDomainException("اعتبارنامه یافت نشد");var now=DateTimeOffset.UtcNow;x.Revoke(now);repository.AddAudit(AuditRecord.Create("credential.revoked","TerminalCredential",x.Id,r.ActorId,now));await repository.SaveChangesAsync(ct);return Unit.Value;}
}
public sealed class ConfigureWebhookHandler(ICreditPaymentsRepository repository,IWebhookSecretProtector protector):IRequestHandler<ConfigureWebhookCommand,ConfiguredWebhookDto>
{
    public async Task<ConfiguredWebhookDto> Handle(ConfigureWebhookCommand r,CancellationToken ct){var x=await repository.GetTerminalAsync(r.TerminalId,true,ct)??throw new CreditPaymentsDomainException("پایانه یافت نشد");if(x.Version!=r.ExpectedVersion)throw new CreditPaymentsDomainException("نسخه پایانه تغییر کرده است");var material=protector.GenerateAndProtect();var now=DateTimeOffset.UtcNow;var version=(x.WebhookEndpoint?.SigningSecretVersion??0)+1;var w=x.ConfigureWebhook(r.Url,material.Ciphertext,version,now);repository.AddAudit(AuditRecord.Create(version==1?"webhook.configured":"webhook.secret.rotated","WebhookEndpoint",w.Id,r.ActorId,now));await repository.SaveChangesAsync(ct);return new(new(w.Id,w.Url,(short)w.Status,w.SigningSecretVersion,w.CreatedAt,w.UpdatedAt),material.Secret);}
}
public sealed class DisableWebhookHandler(ICreditPaymentsRepository repository):IRequestHandler<DisableWebhookCommand,TerminalDto>
{
    public async Task<TerminalDto> Handle(DisableWebhookCommand r,CancellationToken ct){var x=await repository.GetTerminalAsync(r.TerminalId,true,ct)??throw new CreditPaymentsDomainException("پایانه یافت نشد");if(x.Version!=r.ExpectedVersion)throw new CreditPaymentsDomainException("نسخه پایانه تغییر کرده است");var now=DateTimeOffset.UtcNow;x.DisableWebhook(now);repository.AddAudit(AuditRecord.Create("webhook.disabled","WebhookEndpoint",x.WebhookEndpoint!.Id,r.ActorId,now));await repository.SaveChangesAsync(ct);return x.ToDto();}
}
public sealed class MerchantCredentialAuthenticator(ICreditPaymentsRepository repository,ICredentialSecretService secrets):IMerchantCredentialAuthenticator
{
    public async Task<MerchantAuthenticationContext?> AuthenticateAsync(string clientId,string secret,CancellationToken ct)=>(await AuthenticateDetailedAsync(clientId,secret,ct)).Context;
    public async Task<MerchantAuthenticationResult> AuthenticateDetailedAsync(string clientId,string secret,CancellationToken ct)
    {
        var graph=await repository.GetAuthenticationGraphAsync(clientId,ct);var now=DateTimeOffset.UtcNow;
        if(graph is null){await repository.RecordAuthenticationFailureAsync("merchant_auth_failed",null,null,now,ct);return new(null,"merchant_auth_failed");}
        var credential=graph.Value.Credential;
        string? category=credential.Status==CredentialStatus.Revoked?"credential_revoked_use_attempt":
            credential.IsExpired(now)?"credential_expired_use_attempt":null;
        var valid=category is null&&graph.Value.Terminal.Status is TerminalStatus.Active or TerminalStatus.Suspended&&
            graph.Value.Merchant.Status is MerchantStatus.Active or MerchantStatus.Suspended&&
            secrets.Verify(secret,credential.SecretHash,credential.SecretHashAlgorithm,credential.SecretHashVersion);
        if(!valid)
        {
            category??="merchant_auth_failed";
            await repository.RecordAuthenticationFailureAsync(category,credential.Id,graph.Value.Terminal.Id,now,ct);
            return new(null,category,credential.Id,graph.Value.Terminal.Id);
        }
        credential.RecordUsed(now);await repository.SaveChangesAsync(ct);
        return new(new(graph.Value.Merchant.Id,graph.Value.Terminal.Id,credential.Id,
            credential.Scopes.ScopeArray().ToHashSet(StringComparer.Ordinal),
            graph.Value.Merchant.Status.ToString().ToLowerInvariant(),graph.Value.Terminal.Status.ToString().ToLowerInvariant()),null);
    }
}
