using FluentValidation;
using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Application.Features.Checkout.Prepare;

namespace Refahi.Modules.Entertainments.Application.Features.Checkout.Sessions;

public sealed record EntertainmentSessionPassengerInput(Guid CartItemId, IReadOnlyList<EntertainmentPassenger> Passengers);
public sealed record StartEntertainmentSessionCommand(Guid UserId, string ProviderKey, string IdempotencyKey,
    IReadOnlyList<EntertainmentSessionPassengerInput> Passengers, string? CartVersion = null) : IRequest<EntertainmentSessionDto>;
public sealed record GetEntertainmentSessionQuery(Guid UserId, Guid SessionId) : IRequest<EntertainmentSessionDto>;
public sealed record FindEntertainmentSessionQuery(Guid UserId, string IdempotencyKey) : IRequest<EntertainmentSessionDto?>;
public sealed record ConfirmEntertainmentSessionCommand(Guid UserId, Guid SessionId, string Version) : IRequest<PrepareEntertainmentCheckoutResponse>;
public sealed record ReleaseEntertainmentSessionCommand(Guid UserId, Guid SessionId) : IRequest;
public sealed record EntertainmentSessionItemDto(Guid CartItemId, string ProgramTitle, string OptionTitle, int Quantity, long UnitPriceMinor);
public sealed record EntertainmentSessionDto(Guid Id, string ProviderKey, string Status, string Version, DateTimeOffset? PayableUntil,
    long TotalAmountMinor, IReadOnlyList<EntertainmentSessionItemDto> Items, Guid? EntertainmentPurchaseId);

public sealed class StartEntertainmentSessionValidator : AbstractValidator<StartEntertainmentSessionCommand>
{
    public StartEntertainmentSessionValidator()
    {
        RuleFor(x => x.CartVersion).MaximumLength(64).WithMessage("اطلاعات درخواست معتبر نیست");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("اطلاعات درخواست معتبر نیست");
        RuleFor(x => x.ProviderKey).NotEmpty().MaximumLength(80).WithMessage("اطلاعات درخواست معتبر نیست");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(160).WithMessage("اطلاعات درخواست معتبر نیست");
        RuleFor(x => x.Passengers).NotNull().Must(x => x != null && x.Count <= 100 && x.Select(y => y.CartItemId).Distinct().Count() == x.Count)
            .WithMessage("اطلاعات درخواست معتبر نیست");
        RuleForEach(x => x.Passengers).ChildRules(line =>
        {
            line.RuleFor(x => x.CartItemId).NotEmpty().WithMessage("اطلاعات درخواست معتبر نیست");
            line.RuleFor(x => x.Passengers).NotNull().Must(x => x != null && x.Count <= 100).WithMessage("اطلاعات درخواست معتبر نیست");
            line.RuleForEach(x => x.Passengers).ChildRules(p =>
            {
                p.RuleFor(x => x.Name).NotNull().MaximumLength(255).WithMessage("اطلاعات درخواست معتبر نیست");
                p.RuleFor(x => x.IdentityNumber).MaximumLength(100).WithMessage("اطلاعات درخواست معتبر نیست");
            });
        });
    }
}

public sealed class ConfirmEntertainmentSessionValidator : AbstractValidator<ConfirmEntertainmentSessionCommand>
{
    public ConfirmEntertainmentSessionValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("اطلاعات درخواست معتبر نیست");
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("اطلاعات درخواست معتبر نیست");
        RuleFor(x => x.Version).NotEmpty().Length(64).WithMessage("اطلاعات درخواست معتبر نیست");
    }
}
public sealed class GetEntertainmentSessionValidator : AbstractValidator<GetEntertainmentSessionQuery>
{
    public GetEntertainmentSessionValidator() { RuleFor(x => x.SessionId).NotEmpty().WithMessage("اطلاعات درخواست معتبر نیست"); }
}
public sealed class ReleaseEntertainmentSessionValidator : AbstractValidator<ReleaseEntertainmentSessionCommand>
{
    public ReleaseEntertainmentSessionValidator() { RuleFor(x => x.SessionId).NotEmpty().WithMessage("اطلاعات درخواست معتبر نیست"); }
}
public sealed class FindEntertainmentSessionValidator : AbstractValidator<FindEntertainmentSessionQuery>
{
    public FindEntertainmentSessionValidator() { RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(160).WithMessage("اطلاعات درخواست معتبر نیست"); }
}
