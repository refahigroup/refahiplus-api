using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.Cart;

namespace Refahi.Modules.Store.Application.Features.Cart.OfferCartV3;

public sealed class UpdateOfferCartItemCommandValidator : AbstractValidator<UpdateOfferCartItemCommand>
{
    public UpdateOfferCartItemCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("شناسه کاربر الزامی است");
        RuleFor(x => x.ModuleId).GreaterThan(0).WithMessage("ماژول فروشگاه نامعتبر است");
        RuleFor(x => x.CartItemId).NotEmpty().WithMessage("شناسه آیتم سبد الزامی است");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("تعداد باید بیشتر از صفر باشد");
    }
}

public sealed class RemoveOfferCartItemCommandValidator : AbstractValidator<RemoveOfferCartItemCommand>
{
    public RemoveOfferCartItemCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("شناسه کاربر الزامی است");
        RuleFor(x => x.ModuleId).GreaterThan(0).WithMessage("ماژول فروشگاه نامعتبر است");
        RuleFor(x => x.CartItemId).NotEmpty().WithMessage("شناسه آیتم سبد الزامی است");
    }
}

public sealed class ReconfirmOfferCartCommandValidator : AbstractValidator<ReconfirmOfferCartCommand>
{
    public ReconfirmOfferCartCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("شناسه کاربر الزامی است");
        RuleFor(x => x.ModuleId).GreaterThan(0).WithMessage("ماژول فروشگاه نامعتبر است");
    }
}

public sealed class SyncOfferCartCommandValidator : AbstractValidator<SyncOfferCartCommand>
{
    public SyncOfferCartCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("شناسه کاربر الزامی است");
        RuleFor(x => x.ModuleId).GreaterThan(0).WithMessage("ماژول فروشگاه نامعتبر است");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128)
            .WithMessage("کلید همگام‌سازی معتبر الزامی است");
        RuleFor(x => x.Items).NotEmpty().WithMessage("حداقل یک آیتم برای همگام‌سازی الزامی است");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.OfferId).NotEmpty().WithMessage("شناسه پیشنهاد الزامی است");
            item.RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("تعداد باید بیشتر از صفر باشد");
            item.RuleFor(x => x.SnapshotOriginalUnitPriceMinor).GreaterThan(0)
                .WithMessage("قیمت اصلی snapshot الزامی است");
            item.RuleFor(x => x.SnapshotFinalUnitPriceMinor).GreaterThanOrEqualTo(0)
                .WithMessage("قیمت نهایی snapshot نامعتبر است");
        });
    }
}
