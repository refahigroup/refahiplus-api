using FluentValidation;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Application.Validation;
namespace Refahi.Modules.Pools.Application.Features.Purchases.Checkout;
public sealed class CheckoutPoolPurchaseValidator : AbstractValidator<CheckoutPoolPurchaseCommand>
{
    public CheckoutPoolPurchaseValidator() { RuleFor(x => x.Id).NotEmpty().WithMessage("خرید معتبر نیست"); RuleFor(x => x.UserId).NotEmpty().WithMessage("کاربر معتبر نیست"); RuleFor(x => x.Version).GreaterThan(0).WithMessage("نسخه خرید معتبر نیست"); RuleFor(x => x.TotalMinor).GreaterThan(0).WithMessage("مبلغ معتبر نیست"); }
}
