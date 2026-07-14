using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;

namespace Refahi.Modules.Store.Application.Features.Products.UpdateVariantAttribute;

public sealed class UpdateVariantAttributeCommandValidator : AbstractValidator<UpdateVariantAttributeCommand>
{
    public UpdateVariantAttributeCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("شناسه محصول الزامی است");

        RuleFor(x => x.AttributeId)
            .NotEmpty().WithMessage("شناسه ویژگی الزامی است");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام ویژگی الزامی است")
            .MaximumLength(100).WithMessage("نام ویژگی نمی‌تواند بیشتر از ۱۰۰ کاراکتر باشد");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("ترتیب نمایش نمی‌تواند منفی باشد");
    }
}
