using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;

namespace Refahi.Modules.Store.Application.Features.Products.RemoveProductImage;

public class RemoveProductImageCommandValidator : AbstractValidator<RemoveProductImageCommand>
{
    public RemoveProductImageCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("شناسه محصول الزامی است");

        RuleFor(x => x.ImageId)
            .GreaterThan(0).WithMessage("شناسه تصویر معتبر نیست");
    }
}
