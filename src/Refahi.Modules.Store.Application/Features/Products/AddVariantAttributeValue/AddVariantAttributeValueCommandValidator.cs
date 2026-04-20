using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;

namespace Refahi.Modules.Store.Application.Features.Products.AddVariantAttributeValue;

public class AddVariantAttributeValueCommandValidator : AbstractValidator<AddVariantAttributeValueCommand>
{
    public AddVariantAttributeValueCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("شناسه محصول الزامی است");

        RuleFor(x => x.AttributeId)
            .NotEmpty().WithMessage("شناسه ویژگی الزامی است");

        RuleFor(x => x.Value)
            .NotEmpty().WithMessage("مقدار ویژگی الزامی است")
            .MaximumLength(200).WithMessage("مقدار ویژگی نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("ترتیب نمایش نمی‌تواند منفی باشد");
    }
}
