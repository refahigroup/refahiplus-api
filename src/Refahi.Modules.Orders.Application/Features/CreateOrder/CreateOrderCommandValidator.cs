using FluentValidation;
using Refahi.Modules.Orders.Application.Contracts.Commands;

namespace Refahi.Modules.Orders.Application.Features.CreateOrder;

public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("شناسه کاربر الزامی است");

        RuleFor(x => x.SourceModule)
            .NotEmpty().WithMessage("ماژول مبدا الزامی است")
            .MaximumLength(50).WithMessage("ماژول مبدا نمی‌تواند بیشتر از ۵۰ کاراکتر باشد");

        RuleFor(x => x.SourceReferenceId)
            .NotEmpty().WithMessage("شناسه مرجع مبدا الزامی است");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithMessage("کلید یکتایی الزامی است")
            .MaximumLength(200).WithMessage("کلید یکتایی نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("سفارش باید حداقل یک آیتم داشته باشد");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Title)
                .NotEmpty().WithMessage("عنوان آیتم الزامی است");

            item.RuleFor(i => i.UnitPriceMinor)
                .GreaterThan(0).WithMessage("قیمت واحد باید بزرگ‌تر از صفر باشد");

            item.RuleFor(i => i.Quantity)
                .GreaterThan(0).WithMessage("تعداد باید بزرگ‌تر از صفر باشد");

            item.RuleFor(i => i.DiscountAmountMinor)
                .GreaterThanOrEqualTo(0).WithMessage("مبلغ تخفیف نمی‌تواند منفی باشد");

            item.RuleFor(i => i.CategoryCode)
                .NotEmpty().WithMessage("کد دسته‌بندی الزامی است");
        });
    }
}
