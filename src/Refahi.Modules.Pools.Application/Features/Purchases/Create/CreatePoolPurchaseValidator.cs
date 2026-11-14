using FluentValidation;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Application.Validation;
namespace Refahi.Modules.Pools.Application.Features.Purchases.Create;
public sealed class CreatePoolPurchaseValidator : AbstractValidator<CreatePoolPurchaseCommand>
{
    public CreatePoolPurchaseValidator() { RuleFor(x => x.UserId).NotEmpty().WithMessage("کاربر معتبر نیست"); RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128).WithMessage("کلید درخواست معتبر نیست"); RuleFor(x => x.ExpectedTotalMinor).GreaterThan(0).WithMessage("مبلغ معتبر نیست"); RuleFor(x => x.Choices).NotNull().WithMessage("انتخاب‌ها الزامی است").Must(x => x != null && x.Count is >= 1 and <= 20 && x.All(y => y != null) && x.Select(y => y.SessionId).Distinct().Count() == x.Count).WithMessage("انتخاب‌ها باید یکتا و بین یک تا بیست باشند"); RuleForEach(x => x.Choices).SetValidator(new PoolChoiceValidator()); }
}
