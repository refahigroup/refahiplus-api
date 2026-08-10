using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Vendor;

namespace Refahi.Modules.Store.Application.Features.Vendor;

public sealed class StartInPersonOrderCommandValidator
    : AbstractValidator<StartInPersonOrderCommand>
{
    public StartInPersonOrderCommandValidator()
    {
        RuleFor(x => x.VendorUserId).NotEmpty().WithMessage("شناسه کاربر Vendor الزامی است");
        RuleFor(x => x.ShopId).NotEmpty().WithMessage("شناسه فروشگاه الزامی است");
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("شناسه محصول الزامی است");
        RuleFor(x => x.MobileNumber)
            .NotEmpty()
            .MaximumLength(20)
            .WithMessage("شماره موبایل معتبر الزامی است");
        RuleFor(x => x.AmountMinor).GreaterThan(0).WithMessage("مبلغ فروش باید بیشتر از صفر باشد");
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(64)
            .WithMessage("کلید یکتای معتبر الزامی است");
    }
}

public sealed class StartUserInPersonOrderCommandValidator
    : AbstractValidator<StartUserInPersonOrderCommand>
{
    public StartUserInPersonOrderCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("شناسه کاربر الزامی است");
        RuleFor(x => x.ShopId).NotEmpty().WithMessage("شناسه فروشگاه الزامی است");
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("شناسه محصول الزامی است");
        RuleFor(x => x.AmountMinor).GreaterThan(0).WithMessage("مبلغ فروش باید بیشتر از صفر باشد");
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(64)
            .WithMessage("کلید یکتای معتبر الزامی است");
    }
}

public sealed class VerifyInPersonOrderCommandValidator
    : AbstractValidator<VerifyInPersonOrderCommand>
{
    public VerifyInPersonOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty().WithMessage("شناسه سفارش الزامی است");
        RuleFor(x => x.OtpReferenceCode)
            .NotEmpty()
            .MaximumLength(2048)
            .WithMessage("مرجع کد تایید الزامی است");
        RuleFor(x => x.OtpCode).NotEmpty().MaximumLength(16).WithMessage("کد تایید الزامی است");
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(64)
            .WithMessage("کلید یکتای معتبر الزامی است");
    }
}
