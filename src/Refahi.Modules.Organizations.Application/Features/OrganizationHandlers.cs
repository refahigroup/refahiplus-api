using FluentValidation;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Organizations.Application.Contracts;
using Refahi.Modules.Organizations.Domain;

namespace Refahi.Modules.Organizations.Application.Features;

public sealed class SaveOrganizationValidator : AbstractValidator<SaveOrganizationCommand>
{
    public SaveOrganizationValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50).WithMessage("کد سازمان معتبر نیست");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200).WithMessage("عنوان سازمان معتبر نیست");
    }
}
public sealed class AddMemberValidator : AbstractValidator<AddOrganizationMemberCommand>
{
    public AddMemberValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("شناسه سازمان الزامی است");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("شناسه کاربر الزامی است");
        RuleFor(x => x).Must(x => x.ValidUntil == null || x.ValidUntil > x.ValidFrom).WithMessage("بازه عضویت معتبر نیست");
    }
}
public sealed class OrganizationHandlers(IOrganizationStore store, ISender sender, TimeProvider clock) :
    IRequestHandler<GetActiveUserOrganizationsQuery, Guid[]>,
    IRequestHandler<ListOrganizationsQuery, List<OrganizationDto>>,
    IRequestHandler<SaveOrganizationCommand, OrganizationDto>,
    IRequestHandler<ListOrganizationMembersQuery, List<MembershipDto>>,
    IRequestHandler<AddOrganizationMemberCommand, MembershipDto>,
    IRequestHandler<RevokeOrganizationMemberCommand>
{
    public Task<Guid[]> Handle(GetActiveUserOrganizationsQuery r, CancellationToken ct) => store.ActiveOrganizationsAsync(r.UserId, clock.GetUtcNow(), ct);
    public async Task<List<OrganizationDto>> Handle(ListOrganizationsQuery r, CancellationToken ct) => (await store.ListAsync(ct)).Select(Map).ToList();
    public async Task<OrganizationDto> Handle(SaveOrganizationCommand r, CancellationToken ct)
    {
        await using var tx = await store.LockAsync(ct);
        if (await store.CodeExistsAsync(r.Code.Trim().ToUpperInvariant(), r.Id, ct)) throw new ValidationException("کد سازمان تکراری است");
        var value = r.Id.HasValue ? await store.GetAsync(r.Id.Value, ct) ?? throw new ValidationException("سازمان یافت نشد") : Organization.Create(r.Code, r.Title);
        value.Update(r.Code, r.Title, r.IsActive);
        if (!r.Id.HasValue) store.Add(value);
        store.Add(OrganizationAudit.Create(r.ActorId, value.Id, "OrganizationSaved", System.Text.Json.JsonSerializer.Serialize(new { value.Code, value.Title, value.IsActive }), clock.GetUtcNow()));
        await store.SaveAsync(ct); await tx.CommitAsync(ct); return Map(value);
    }
    public async Task<List<MembershipDto>> Handle(ListOrganizationMembersQuery r, CancellationToken ct) => (await store.MembersAsync(r.OrganizationId, ct)).Select(Map).ToList();
    public async Task<MembershipDto> Handle(AddOrganizationMemberCommand r, CancellationToken ct)
    {
        await using var tx = await store.LockAsync(ct);
        var org = await store.GetAsync(r.OrganizationId, ct);
        if (org is null || !org.IsActive) throw new ValidationException("سازمان فعال یافت نشد");
        if (!(await sender.Send(new GetOrderUserSummariesQuery([r.UserId]), ct)).Any(x => x.UserId == r.UserId)) throw new ValidationException("کاربر یافت نشد");
        var members = await store.MembersAsync(r.OrganizationId, ct);
        if (members.Any(x => x.UserId == r.UserId && x.RevokedAt == null && (x.ValidUntil == null || x.ValidUntil > r.ValidFrom)
            && (r.ValidUntil == null || r.ValidUntil > x.ValidFrom))) throw new ValidationException("عضویت هم‌پوشان برای کاربر وجود دارد");
        var value = OrganizationMembership.Create(r.OrganizationId, r.UserId, r.ActorId, r.ValidFrom, r.ValidUntil);
        store.Add(OrganizationAudit.Create(r.ActorId, value.Id, "MembershipCreated", System.Text.Json.JsonSerializer.Serialize(new { r.OrganizationId, r.UserId, r.ValidFrom, r.ValidUntil }), clock.GetUtcNow()));
        store.Add(value); await store.SaveAsync(ct); await tx.CommitAsync(ct); return Map(value);
    }
    public async Task<Unit> Handle(RevokeOrganizationMemberCommand r, CancellationToken ct)
    {
        await using var tx = await store.LockAsync(ct);
        var value = await store.MembershipAsync(r.MembershipId, ct);
        if (value is null || value.OrganizationId != r.OrganizationId) throw new ValidationException("عضویت یافت نشد");
        store.Add(OrganizationAudit.Create(r.ActorId, value.Id, "MembershipRevoked", System.Text.Json.JsonSerializer.Serialize(new { r.OrganizationId, value.UserId }), clock.GetUtcNow()));
        value.Revoke(r.ActorId, clock.GetUtcNow()); await store.SaveAsync(ct); await tx.CommitAsync(ct); return Unit.Value;
    }
    private static OrganizationDto Map(Organization x) => new(x.Id, x.Code, x.Title, x.IsActive);
    private static MembershipDto Map(OrganizationMembership x) => new(x.Id, x.OrganizationId, x.UserId, x.ValidFrom, x.ValidUntil, x.RevokedAt);
}
