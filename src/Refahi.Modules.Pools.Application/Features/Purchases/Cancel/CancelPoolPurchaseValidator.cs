using FluentValidation;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Application.Validation;
namespace Refahi.Modules.Pools.Application.Features.Purchases.Cancel;
public sealed class CancelPoolPurchaseValidator : AbstractValidator<CancelPoolPurchaseCommand>
{
    public CancelPoolPurchaseValidator() { RuleFor(x => x.Id).NotEmpty().WithMessage("خرید معتبر نیست"); RuleFor(x => x.UserId).NotEmpty().WithMessage("کاربر معتبر نیست"); }
}
