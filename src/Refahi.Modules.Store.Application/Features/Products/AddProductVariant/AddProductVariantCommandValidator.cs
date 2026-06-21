using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Modules.Store.Domain.Enums;

namespace Refahi.Modules.Store.Application.Features.Products.AddProductVariant;

public class AddProductVariantCommandValidator : AbstractValidator<AddProductVariantCommand>
{
    public AddProductVariantCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("شناسه محصول الزامی است");

        RuleFor(x => x.Combinations)
            .NotNull().WithMessage("ترکیب‌های تنوع الزامی است");

        RuleFor(x => x.StockCount)
            .GreaterThanOrEqualTo(0).WithMessage("تعداد موجودی نمی‌تواند منفی باشد");

        RuleFor(x => x.PriceMinor)
            .GreaterThan(0).WithMessage("قیمت باید بیشتر از صفر باشد");

        RuleFor(x => x.DiscountedPriceMinor)
            .GreaterThan(0).When(x => x.DiscountedPriceMinor.HasValue)
            .WithMessage("قیمت تخفیف‌خورده باید بیشتر از صفر باشد");

        RuleFor(x => x)
            .Must(x => !x.DiscountedPriceMinor.HasValue || x.DiscountedPriceMinor.Value < x.PriceMinor)
            .WithMessage("قیمت تخفیف‌خورده باید کمتر از قیمت اصلی باشد");

        RuleFor(x => x)
            .Must(x => x.FromDate.HasValue == x.ToDate.HasValue)
            .WithMessage("تاریخ شروع و پایان اعتبار باید همزمان ثبت شوند");

        RuleFor(x => x)
            .Must(x => !x.FromDate.HasValue || !x.ToDate.HasValue || x.FromDate.Value <= x.ToDate.Value)
            .WithMessage("تاریخ شروع اعتبار باید قبل از تاریخ پایان باشد");

        RuleFor(x => x.CapacityType)
            .IsInEnum().WithMessage("نوع ظرفیت تنوع معتبر نیست");

        RuleFor(x => x.Capacity)
            .GreaterThan(0)
            .When(x => x.CapacityType is VariantCapacityType.TotalPeriod or VariantCapacityType.PerEligibleDay)
            .WithMessage("ظرفیت تنوع باید بیشتر از صفر باشد");
    }
}
