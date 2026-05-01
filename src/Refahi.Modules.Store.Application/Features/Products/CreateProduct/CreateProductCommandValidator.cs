using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;

namespace Refahi.Modules.Store.Application.Features.Products.CreateProduct;

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.AgreementProductId)
            .NotEmpty().WithMessage("شناسه قرارداد محصول الزامی است");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("عنوان محصول الزامی است")
            .MaximumLength(300).WithMessage("عنوان محصول نمی‌تواند بیشتر از ۳۰۰ کاراکتر باشد");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("اسلاگ الزامی است")
            .MaximumLength(300).WithMessage("اسلاگ نمی‌تواند بیشتر از ۳۰۰ کاراکتر باشد")
            .Matches(@"^[a-z0-9-]+$").WithMessage("اسلاگ فقط می‌تواند شامل حروف کوچک انگلیسی، اعداد و خط تیره باشد");

        RuleFor(x => x.StockCount)
            .GreaterThanOrEqualTo(0).WithMessage("تعداد موجودی نمی‌تواند منفی باشد");
    }
}
