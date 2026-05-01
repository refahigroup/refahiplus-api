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
            .MaximumLength(300).WithMessage("عنوان محصول نمی‌تواند بیشتر از ۳۰۰ کاراکتر باشد");
    }
}
