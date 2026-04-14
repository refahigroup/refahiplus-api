using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;

namespace Refahi.Modules.Store.Application.Features.Products.UpdateProduct;

public class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("شناسه محصول الزامی است");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("عنوان محصول الزامی است")
            .MaximumLength(500).WithMessage("عنوان محصول نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد");

        RuleFor(x => x.PriceMinor)
            .GreaterThan(0).WithMessage("قیمت محصول باید بیشتر از صفر باشد");
    }
}
