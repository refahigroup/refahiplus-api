using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using MediatR;
using Refahi.Modules.Organizations.Application.Contracts;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Promotions.Application.Contracts;
using Refahi.Modules.Promotions.Domain;

namespace Refahi.Modules.Promotions.Application.Features;

public sealed class PromotionHandlers(IPromotionStore store, ISender sender, IPromotionCodeProtector protector, TimeProvider clock) :
    IRequestHandler<SaveCampaignCommand, CampaignDto>, IRequestHandler<SetCampaignActiveCommand>,
    IRequestHandler<ListCampaignsQuery, List<CampaignDto>>, IRequestHandler<GetCampaignQuery, CampaignDto>,
    IRequestHandler<GenerateCodesCommand, List<CodeDto>>, IRequestHandler<ListCodesQuery, List<CodeDto>>,
    IRequestHandler<ExportCodesCommand, List<CodeExport>>, IRequestHandler<DisableCodeCommand>,
    IRequestHandler<ListRedemptionsQuery, List<RedemptionDto>>,
    IRequestHandler<PreviewPromotionQuery, PromotionQuote>, IRequestHandler<ReservePromotionCommand, PromotionReservation>,
    IRequestHandler<BeginPromotionPaymentCommand>, IRequestHandler<ReleasePromotionCommand>,
    IRequestHandler<CompletePromotionCommand>, IRequestHandler<ReversePromotionCommand>,
    IRequestHandler<GetPromotionReservationQuery, PromotionReservation?>,
    IRequestHandler<GetPendingPromotionsQuery, List<PromotionReservation>>
{
    public async Task<CampaignDto> Handle(SaveCampaignCommand r, CancellationToken ct)
    {
        var v = r.Rules;
        if (!Enum.TryParse<DiscountKind>(v.Kind, true, out var kind)) throw new PromotionException("نوع تخفیف معتبر نیست");
        if (v.OrganizationIds.Length > 0)
        {
            var orgs = await sender.Send(new ListOrganizationsQuery(), ct);
            if (v.OrganizationIds.Any(id => !orgs.Any(o => o.Id == id && o.IsActive))) throw new PromotionException("سازمان فعال یافت نشد");
        }
        await ValidateUsers(v.UserIds, ct);
        var campaign = r.Id.HasValue ? await RequireCampaign(r.Id.Value, ct) : PromotionCampaign.Create(v.Title, kind, v.FixedAmountMinor, v.Percent, v.MaximumDiscountMinor, v.MinimumEligibleMinor, v.BudgetMinor, v.MaximumUses, v.MaximumUsesPerUser, v.SourceModules, v.CategoryPrefixes, v.OrganizationIds, v.UserIds, v.StartsAt, v.EndsAt);
        await using var tx = await store.LockCampaignAsync(campaign.Id, ct);
        if (r.Id.HasValue) campaign.Update(v.Title, kind, v.FixedAmountMinor, v.Percent, v.MaximumDiscountMinor, v.MinimumEligibleMinor, v.BudgetMinor, v.MaximumUses, v.MaximumUsesPerUser, v.SourceModules, v.CategoryPrefixes, v.OrganizationIds, v.UserIds, v.StartsAt, v.EndsAt);
        else store.Add(campaign);
        store.Add(PromotionAudit.Create(r.ActorId, campaign.Id, "CampaignSaved", clock.GetUtcNow(), JsonSerializer.Serialize(r.Rules)));
        await store.SaveAsync(ct); await tx.CommitAsync(ct); return await Map(campaign, ct);
    }
    public async Task<Unit> Handle(SetCampaignActiveCommand r, CancellationToken ct)
    {
        await using var tx = await store.LockCampaignAsync(r.Id, ct);
        (await RequireCampaign(r.Id, ct)).SetActive(r.Active, clock.GetUtcNow());
        store.Add(PromotionAudit.Create(r.ActorId, r.Id, r.Active ? "CampaignActivated" : "CampaignPaused", clock.GetUtcNow()));
        await store.SaveAsync(ct); await tx.CommitAsync(ct); return Unit.Value;
    }
    public async Task<List<CampaignDto>> Handle(ListCampaignsQuery r, CancellationToken ct)
    {
        var result = new List<CampaignDto>();
        foreach (var x in await store.CampaignsAsync(r.Search, (r.Page - 1) * r.PageSize, r.PageSize, ct)) result.Add(await Map(x, ct));
        return result;
    }
    public async Task<CampaignDto> Handle(GetCampaignQuery r, CancellationToken ct) => await Map(await RequireCampaign(r.Id, ct), ct);
    public async Task<List<CodeDto>> Handle(GenerateCodesCommand r, CancellationToken ct)
    {
        await ValidateUsers(r.UserId.HasValue ? [r.UserId.Value] : [], ct);
        await using var tx = await store.LockCampaignAsync(r.CampaignId, ct);
        await RequireCampaign(r.CampaignId, ct);
        var fingerprint = Hash(JsonSerializer.Serialize(new { r.PublicCode, r.UserId, r.Count, r.MaximumUses }));
        var batchKey = r.IdempotencyKey + ":" + fingerprint;
        var existing = await store.BatchCodesAsync(r.CampaignId, r.IdempotencyKey + ":", false, ct);
        var replay = existing.Where(x => x.BatchKey == batchKey).ToList();
        if (replay.Count > 0) return replay.Select(Map).ToList();
        if (existing.Any(x => x.BatchKey.StartsWith(r.IdempotencyKey + ":", StringComparison.Ordinal))) throw new PromotionException("کلید درخواست با محتوای دیگری استفاده شده است", 409);
        var result = new List<CodeDto>();
        for (var i = 0; i < r.Count; i++)
        {
            var code = r.PublicCode == null ? RandomCode() : Normalize(r.PublicCode);
            if (await store.CodeByHashAsync(Hash(code), ct) != null) throw new PromotionException("کد تخفیف تکراری است", 409);
            var value = PromotionCode.Create(r.CampaignId, Hash(code), protector.Protect(code), "••••" + code[^Math.Min(4, code.Length)..], r.UserId, r.MaximumUses, batchKey);
            store.Add(value); result.Add(Map(value));
        }
        store.Add(PromotionAudit.Create(r.ActorId, r.CampaignId, "CodesGenerated", clock.GetUtcNow(), batchKey));
        await store.SaveAsync(ct); await tx.CommitAsync(ct); return result.OrderBy(x => x.Id).ToList();
    }
    public async Task<List<CodeDto>> Handle(ListCodesQuery r, CancellationToken ct) => (await store.CodesAsync(r.CampaignId, (r.Page - 1) * r.PageSize, r.PageSize, ct)).Select(Map).ToList();
    public async Task<List<CodeExport>> Handle(ExportCodesCommand r, CancellationToken ct)
    {
        var codes = await store.BatchCodesAsync(r.CampaignId, r.BatchKey, true, ct);
        store.Add(PromotionAudit.Create(r.ActorId, r.CampaignId, "CodesExported", clock.GetUtcNow(), r.BatchKey));
        await store.SaveAsync(ct); return codes.Select(x => new CodeExport(x.Id, protector.Unprotect(x.Ciphertext), x.UserId)).ToList();
    }
    public async Task<Unit> Handle(DisableCodeCommand r, CancellationToken ct)
    {
        await using var tx = await store.LockCampaignAsync(r.CampaignId, ct);
        var code = await store.CodeAsync(r.CodeId, ct);
        if (code == null || code.CampaignId != r.CampaignId) throw new PromotionException("کد یافت نشد", 404);
        code.Disable(); store.Add(PromotionAudit.Create(r.ActorId, code.Id, "CodeDisabled", clock.GetUtcNow()));
        await store.SaveAsync(ct); await tx.CommitAsync(ct); return Unit.Value;
    }
    public async Task<List<RedemptionDto>> Handle(ListRedemptionsQuery r, CancellationToken ct)
    {
        RedemptionStatus? status = r.Status == null ? null : Enum.TryParse<RedemptionStatus>(r.Status, true, out var parsed) ? parsed : throw new PromotionException("وضعیت معتبر نیست");
        return (await store.RedemptionsAsync(r.OrderId, r.UserId, status, (r.Page - 1) * r.PageSize, r.PageSize, ct)).Select(x => new RedemptionDto(x.Id, x.CampaignId, x.CodeId, x.OrderId, x.UserId, x.OrganizationId, x.DiscountMinor, x.Status.ToString(), x.CreatedAt, x.ExpiresAt)).ToList();
    }
    public async Task<PromotionQuote> Handle(PreviewPromotionQuery r, CancellationToken ct)
    {
        var code = await RequireCode(r.Code, r.Context.UserId, ct);
        await using var tx = await store.LockCampaignAsync(code.CampaignId, ct);
        return await Quote(await RequireCampaign(code.CampaignId, ct), code, r.Context, ct);
    }
    public async Task<PromotionReservation> Handle(ReservePromotionCommand r, CancellationToken ct)
    {
        var code = await RequireCode(r.Code, r.Context.UserId, ct);
        await using var tx = await store.LockCampaignAsync(code.CampaignId, ct);
        var fingerprint = Hash(JsonSerializer.Serialize(new { Code = code.Hash, r.Context }));
        var existing = await store.OperationAsync(r.Context.OrderId, r.IdempotencyKey, ct);
        if (existing != null)
        {
            if (existing.Fingerprint != fingerprint) throw new PromotionException("کلید درخواست با محتوای دیگری استفاده شده است", 409);
            return Map(existing);
        }
        var campaign = await RequireCampaign(code.CampaignId, ct);
        var quote = await Quote(campaign, code, r.Context, ct);
        var value = PromotionRedemption.Reserve(campaign.Id, code.Id, r.Context.OrderId, r.Context.UserId, campaign.Version,
            r.IdempotencyKey, fingerprint, new(quote.DiscountMinor, quote.OrganizationId, quote.Items.Select(x => new PromotionShare(x.OrderItemId, x.AmountMinor)).ToArray()), quote.ExpiresAt, clock.GetUtcNow());
        store.Add(value); await store.SaveAsync(ct); await tx.CommitAsync(ct); return Map(value);
    }
    public async Task<Unit> Handle(BeginPromotionPaymentCommand r, CancellationToken ct)
    {
        var initial = await RequireRedemption(r.ReservationId, r.OrderId, ct);
        await using var tx = await store.LockCampaignAsync(initial.CampaignId, ct);
        if (initial.UserId != r.UserId) throw new PromotionException("دسترسی مجاز نیست", 403);
        if (initial.Status == RedemptionStatus.PaymentPending && r.ResumeExistingIntent) return Unit.Value;
        if (initial.ExpiresAt <= clock.GetUtcNow()) throw new PromotionException("مهلت تخفیف به پایان رسیده است", 409);
        var campaign = await RequireCampaign(initial.CampaignId, ct);
        var code = await store.CodeAsync(initial.CodeId, ct);
        if (campaign.Status != CampaignStatus.Active || !code!.IsActive || campaign.EndsAt <= clock.GetUtcNow()) throw new PromotionException("کد تخفیف دیگر معتبر نیست", 409);
        if (initial.OrganizationId.HasValue && !(await sender.Send(new GetActiveUserOrganizationsQuery(r.UserId), ct)).Contains(initial.OrganizationId.Value)) throw new PromotionException("عضویت سازمانی معتبر نیست", 409);
        initial.BeginPayment(clock.GetUtcNow()); await store.SaveAsync(ct); await tx.CommitAsync(ct); return Unit.Value;
    }
    public Task<Unit> Handle(ReleasePromotionCommand r, CancellationToken ct) => Change(r.ReservationId, r.OrderId, x => x.Release(r.Expired), ct);
    public Task<Unit> Handle(CompletePromotionCommand r, CancellationToken ct) => Change(r.ReservationId, r.OrderId, x => x.Commit(r.PaymentId), ct);
    public Task<Unit> Handle(ReversePromotionCommand r, CancellationToken ct) => Change(r.ReservationId, r.OrderId, x => x.Reverse(), ct);
    public async Task<PromotionReservation?> Handle(GetPromotionReservationQuery r, CancellationToken ct) { var value = await store.RedemptionAsync(r.Id, ct); return value == null ? null : Map(value); }
    public async Task<List<PromotionReservation>> Handle(GetPendingPromotionsQuery r, CancellationToken ct) => (await store.PendingAsync(Math.Clamp(r.Take, 1, 200), ct)).Select(Map).ToList();
    private async Task<Unit> Change(Guid id, Guid order, Action<PromotionRedemption> action, CancellationToken ct)
    { var value = await RequireRedemption(id, order, ct); await using var tx = await store.LockCampaignAsync(value.CampaignId, ct); action(value); await store.SaveAsync(ct); await tx.CommitAsync(ct); return Unit.Value; }
    private async Task<PromotionQuote> Quote(PromotionCampaign campaign, PromotionCode code, PromotionContext context, CancellationToken ct)
    {
        code = await store.CodeAsync(code.Id, ct) ?? throw new PromotionException("کد تخفیف معتبر نیست");
        if (!code.IsActive) throw new PromotionException("کد تخفیف معتبر نیست");
        var orgs = campaign.OrganizationIds.Length == 0 ? [] : await sender.Send(new GetActiveUserOrganizationsQuery(context.UserId), ct);
        var calculation = campaign.Calculate(context.UserId, context.SourceModule, orgs, context.Items.Select(x => new PromotionLine(x.OrderItemId, x.CategoryCode, x.AmountMinor)).ToArray(), context.AmountBeforePromotionMinor, clock.GetUtcNow());
        var usage = await store.UsageAsync(campaign.Id, context.UserId, code.Id, ct);
        if (usage.Uses >= campaign.MaximumUses || usage.UserUses >= campaign.MaximumUsesPerUser || usage.CodeUses >= code.MaximumUses || calculation.DiscountMinor > campaign.BudgetMinor - usage.BudgetMinor) throw new PromotionException("بودجه یا سهمیه کد تخفیف به پایان رسیده است");
        var expiry = new[] { clock.GetUtcNow().AddMinutes(15), campaign.EndsAt, context.PayableUntil ?? DateTimeOffset.MaxValue }.Min();
        if (expiry <= clock.GetUtcNow()) throw new PromotionException("مهلت سفارش به پایان رسیده است", 409);
        return new(campaign.Id, code.Id, campaign.Version, calculation.DiscountMinor, calculation.OrganizationId, calculation.Shares.Select(x => new PromotionItemDiscount(x.OrderItemId, x.AmountMinor)).ToArray(), expiry);
    }
    private async Task<PromotionCode> RequireCode(string raw, Guid user, CancellationToken ct)
    { var value = await store.CodeByHashAsync(Hash(Normalize(raw)), ct); if (value == null || !value.IsActive || (value.UserId.HasValue && value.UserId != user)) throw new PromotionException("کد تخفیف معتبر نیست"); return value; }
    private async Task<PromotionCampaign> RequireCampaign(Guid id, CancellationToken ct) => await store.CampaignAsync(id, ct) ?? throw new PromotionException("کمپین یافت نشد", 404);
    private async Task<PromotionRedemption> RequireRedemption(Guid id, Guid order, CancellationToken ct)
    { var value = await store.RedemptionAsync(id, ct); if (value == null || value.OrderId != order) throw new PromotionException("رزرو تخفیف یافت نشد", 409); return value; }
    private async Task ValidateUsers(Guid[] ids, CancellationToken ct)
    { if (ids.Length > 0 && (await sender.Send(new GetOrderUserSummariesQuery(ids), ct)).Select(x => x.UserId).Distinct().Count() != ids.Distinct().Count()) throw new PromotionException("کاربر یافت نشد"); }
    private async Task<CampaignDto> Map(PromotionCampaign c, CancellationToken ct)
    { var usage = await store.UsageAsync(c.Id, null, null, ct); return new(c.Id, c.Status.ToString(), c.Version, new(c.Title, c.Kind.ToString(), c.FixedAmountMinor, c.Percent, c.MaximumDiscountMinor, c.MinimumEligibleMinor, c.BudgetMinor, c.MaximumUses, c.MaximumUsesPerUser, c.SourceModules, c.CategoryPrefixes, c.OrganizationIds, c.UserIds, c.StartsAt, c.EndsAt), usage.BudgetMinor, usage.Uses,usage.ReservedBudgetMinor,usage.ConsumedBudgetMinor); }
    private static CodeDto Map(PromotionCode c) => new(c.Id, c.Mask, c.UserId, c.IsActive, c.MaximumUses, c.BatchKey);
    private static PromotionReservation Map(PromotionRedemption r) => new(r.Id, r.OrderId, r.UserId, r.Status.ToString(), new(r.CampaignId, r.CodeId, r.RuleVersion, r.DiscountMinor, r.OrganizationId, r.Items.Select(x => new PromotionItemDiscount(x.OrderItemId, x.AmountMinor)).ToArray(), r.ExpiresAt));
    public static string Normalize(string value)
    { var code = value.Trim().ToUpperInvariant(); if (code.Length is < 1 or > 50 || code.Any(x => !(x is >= 'A' and <= 'Z' or >= '0' and <= '9' or '-'))) throw new PromotionException("قالب کد تخفیف معتبر نیست"); return code; }
    private static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
    private static string RandomCode() => string.Concat(Enumerable.Range(0, 16).Select(_ => "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"[RandomNumberGenerator.GetInt32(32)]));
}

public sealed class GenerateCodesValidator : AbstractValidator<GenerateCodesCommand>
{
    public GenerateCodesValidator()
    {
        RuleFor(x => x.Count).InclusiveBetween(1, 1000).WithMessage("تعداد کد باید بین یک و هزار باشد");
        RuleFor(x => x.MaximumUses).GreaterThan(0).WithMessage("سقف مصرف باید مثبت باشد");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100).WithMessage("کلید درخواست معتبر نیست");
        RuleFor(x => x).Must(x => x.PublicCode == null || x.Count == 1).WithMessage("برای کد عمومی تعداد باید یک باشد");
    }
}
public sealed class CampaignValidator : AbstractValidator<SaveCampaignCommand>
{
    public CampaignValidator()
    {
        RuleFor(x => x.Rules).NotNull().WithMessage("قواعد کمپین الزامی است");
        When(x => x.Rules != null, () =>
        {
            RuleFor(x => x.Rules.SourceModules).NotNull().WithMessage("فهرست ماژول‌ها الزامی است");
            RuleFor(x => x.Rules.CategoryPrefixes).NotNull().WithMessage("فهرست دسته‌بندی‌ها الزامی است");
            RuleFor(x => x.Rules.OrganizationIds).NotNull().WithMessage("فهرست سازمان‌ها الزامی است");
            RuleFor(x => x.Rules.UserIds).NotNull().WithMessage("فهرست کاربران الزامی است");
            RuleFor(x => x.Rules.Title).NotEmpty().MaximumLength(200).WithMessage("عنوان کمپین معتبر نیست");
        });
    }
}
public sealed class RedemptionListValidator : AbstractValidator<ListRedemptionsQuery>
{
    public RedemptionListValidator() { RuleFor(x => x.Page).InclusiveBetween(1, 100000).WithMessage("صفحه معتبر نیست"); RuleFor(x => x.PageSize).InclusiveBetween(1, 200).WithMessage("اندازه صفحه معتبر نیست"); }
}

public sealed class CampaignListValidator : AbstractValidator<ListCampaignsQuery>
{ public CampaignListValidator() { RuleFor(x => x.Page).InclusiveBetween(1, 100000).WithMessage("صفحه معتبر نیست"); RuleFor(x => x.PageSize).InclusiveBetween(1, 200).WithMessage("اندازه صفحه معتبر نیست"); } }
public sealed class CodeListValidator : AbstractValidator<ListCodesQuery>
{ public CodeListValidator() { RuleFor(x => x.Page).InclusiveBetween(1, 100000).WithMessage("صفحه معتبر نیست"); RuleFor(x => x.PageSize).InclusiveBetween(1, 200).WithMessage("اندازه صفحه معتبر نیست"); } }
