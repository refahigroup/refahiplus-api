using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.Cart;

namespace Refahi.Modules.Store.Application.Features.Cart.SyncCart;

public class SyncCartCommandValidator : AbstractValidator<SyncCartCommand>
{
    public SyncCartCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("شناسه کاربر الزامی است");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithMessage("کلید همگام‌سازی الزامی است");

        RuleFor(x => x.Items)
            .NotNull().WithMessage("لیست آیتم‌ها نمی‌تواند خالی باشد")
            .Must(items => items is null || items.Count <= 50)
            .WithMessage("تعداد آیتم‌ها نمی‌تواند بیشتر از ۵۰ عدد باشد");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Quantity)
                .GreaterThan(0).WithMessage("تعداد باید بیشتر از صفر باشد");

            item.RuleFor(i => i.ProductId)
                .NotEmpty().WithMessage("شناسه محصول الزامی است");
        });
    }
}
