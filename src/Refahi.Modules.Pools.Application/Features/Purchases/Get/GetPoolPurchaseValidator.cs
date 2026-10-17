using FluentValidation;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Application.Validation;
namespace Refahi.Modules.Pools.Application.Features.Purchases.Get;
public sealed class GetPoolPurchaseValidator : AbstractValidator<GetPoolPurchaseQuery>
{
    public GetPoolPurchaseValidator() { RuleFor(x => x.Id).NotEmpty().WithMessage("خرید معتبر نیست"); RuleFor(x => x.UserId).NotEmpty().WithMessage("کاربر معتبر نیست"); }
}
