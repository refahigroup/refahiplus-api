using FluentValidation;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Application.Validation;
namespace Refahi.Modules.Pools.Application.Features.Purchases.Quote;
public sealed class QuotePoolPurchaseValidator : AbstractValidator<QuotePoolPurchaseQuery>
{
    public QuotePoolPurchaseValidator() { RuleFor(x => x.Choices).NotNull().WithMessage("انتخاب‌ها الزامی است").Must(x => x != null && x.Count == 1 && x.All(y => y != null) && x.Select(y => y.SessionId).Distinct().Count() == x.Count).WithMessage("هر خرید جدید باید دقیقاً یک سانس داشته باشد"); RuleForEach(x => x.Choices).SetValidator(new PoolChoiceValidator()); }
}
