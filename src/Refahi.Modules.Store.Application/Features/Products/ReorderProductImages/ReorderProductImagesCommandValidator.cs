using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;

namespace Refahi.Modules.Store.Application.Features.Products.ReorderProductImages;

public class ReorderProductImagesCommandValidator : AbstractValidator<ReorderProductImagesCommand>
{
    public ReorderProductImagesCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("شناسه محصول الزامی است");

        RuleFor(x => x.Items)
            .NotNull().WithMessage("لیست ترتیب تصاویر الزامی است")
            .Must(items => items != null && items.Count > 0)
            .WithMessage("حداقل یک تصویر برای مرتب‌سازی لازم است");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ImageId)
                .GreaterThan(0).WithMessage("شناسه تصویر معتبر نیست");
            item.RuleFor(i => i.SortOrder)
                .GreaterThanOrEqualTo(0).WithMessage("ترتیب نمایش نمی‌تواند منفی باشد");
        });
    }
}
