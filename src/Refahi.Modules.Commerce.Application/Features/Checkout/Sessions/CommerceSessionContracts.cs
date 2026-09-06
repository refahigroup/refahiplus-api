using FluentValidation;
using MediatR;
using Refahi.Modules.Commerce.Application.Contracts.Providers;
using Refahi.Modules.Commerce.Application.Features.Checkout.Prepare;

namespace Refahi.Modules.Commerce.Application.Features.Checkout.Sessions;

public sealed record CommerceSessionPassengerInput(Guid CartItemId, IReadOnlyList<CommercePassenger> Passengers);
public sealed record StartCommerceSessionCommand(Guid UserId, string ProviderKey, string IdempotencyKey,
    IReadOnlyList<CommerceSessionPassengerInput> Passengers, string? CartVersion = null) : IRequest<CommerceSessionDto>;
public sealed record GetCommerceSessionQuery(Guid UserId, Guid SessionId) : IRequest<CommerceSessionDto>;
public sealed record FindCommerceSessionQuery(Guid UserId, string IdempotencyKey) : IRequest<CommerceSessionDto?>;
public sealed record ConfirmCommerceSessionCommand(Guid UserId, Guid SessionId, string Version) : IRequest<PrepareCommerceCheckoutResponse>;
public sealed record ReleaseCommerceSessionCommand(Guid UserId, Guid SessionId) : IRequest;
public sealed record CommerceSessionItemDto(Guid CartItemId, string ProductTitle, string OptionTitle, int Quantity, long UnitPriceMinor);
public sealed record CommerceSessionDto(Guid Id, string ProviderKey, string Status, string Version, DateTimeOffset? PayableUntil,
    long TotalAmountMinor, IReadOnlyList<CommerceSessionItemDto> Items, Guid? CommerceOrderId);

public sealed class StartCommerceSessionValidator : AbstractValidator<StartCommerceSessionCommand>
{
    public StartCommerceSessionValidator()
    {
        RuleFor(x => x.CartVersion).MaximumLength(64).WithMessage("نسخه سبد معتبر نیست");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("ع©ط§ط±ط¨ط± ظ…ط¹طھط¨ط± ظ†غŒط³طھ");
        RuleFor(x => x.ProviderKey).NotEmpty().MaximumLength(80).WithMessage("ظ¾ط±ظˆظˆط§غŒط¯ط± ظ…ط¹طھط¨ط± ظ†غŒط³طھ");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(160).WithMessage("ع©ظ„غŒط¯ غŒع©طھط§غŒغŒ ظ…ط¹طھط¨ط± ظ†غŒط³طھ");
        RuleFor(x => x.Passengers).NotNull().Must(x => x != null && x.Count <= 100 && x.Select(y => y.CartItemId).Distinct().Count() == x.Count)
            .WithMessage("ط§ط·ظ„ط§ط¹ط§طھ ظ…ط³ط§ظپط±ط§ظ† ظ…ط¹طھط¨ط± ظ†غŒط³طھ");
        RuleForEach(x => x.Passengers).ChildRules(line =>
        {
            line.RuleFor(x => x.CartItemId).NotEmpty().WithMessage("ط¢غŒطھظ… ظ…ط¹طھط¨ط± ظ†غŒط³طھ");
            line.RuleFor(x => x.Passengers).NotNull().Must(x => x != null && x.Count <= 100).WithMessage("طھط¹ط¯ط§ط¯ ظ…ط³ط§ظپط±ط§ظ† ظ…ط¹طھط¨ط± ظ†غŒط³طھ");
            line.RuleForEach(x => x.Passengers).ChildRules(p =>
            {
                p.RuleFor(x => x.Name).NotNull().MaximumLength(255).WithMessage("ظ†ط§ظ… ظ…ط³ط§ظپط± ظ…ط¹طھط¨ط± ظ†غŒط³طھ");
                p.RuleFor(x => x.IdentityNumber).MaximumLength(100).WithMessage("ط´ظ…ط§ط±ظ‡ ط´ظ†ط§ط³ط§غŒغŒ ظ…ط¹طھط¨ط± ظ†غŒط³طھ");
            });
        });
    }
}

public sealed class ConfirmCommerceSessionValidator : AbstractValidator<ConfirmCommerceSessionCommand>
{
    public ConfirmCommerceSessionValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("کاربر معتبر نیست");
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("رزرو معتبر نیست");
        RuleFor(x => x.Version).NotEmpty().Length(64).WithMessage("نسخه رزرو معتبر نیست");
    }
}
public sealed class GetCommerceSessionValidator : AbstractValidator<GetCommerceSessionQuery>
{
    public GetCommerceSessionValidator() { RuleFor(x => x.SessionId).NotEmpty().WithMessage("رزرو معتبر نیست"); }
}
public sealed class ReleaseCommerceSessionValidator : AbstractValidator<ReleaseCommerceSessionCommand>
{
    public ReleaseCommerceSessionValidator() { RuleFor(x => x.SessionId).NotEmpty().WithMessage("رزرو معتبر نیست"); }
}
public sealed class FindCommerceSessionValidator : AbstractValidator<FindCommerceSessionQuery>
{
    public FindCommerceSessionValidator() { RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(160).WithMessage("کلید یکتایی معتبر نیست"); }
}
