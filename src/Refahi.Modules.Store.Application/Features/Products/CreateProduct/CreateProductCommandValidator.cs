using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;

namespace Refahi.Modules.Store.Application.Features.Products.CreateProduct;

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("عنوان محصول الزامی است")
            .MaximumLength(500).WithMessage("عنوان محصول نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("اسلاگ الزامی است")
            .MaximumLength(500).WithMessage("اسلاگ نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد")
            .Matches(@"^[a-z0-9-]+$").WithMessage("اسلاگ فقط می‌تواند شامل حروف کوچک انگلیسی، اعداد و خط تیره باشد");

        RuleFor(x => x.PriceMinor)
            .GreaterThan(0).WithMessage("قیمت محصول باید بیشتر از صفر باشد");

        RuleFor(x => x.ProductType)
            .InclusiveBetween((short)1, (short)2).WithMessage("نوع محصول نامعتبر است");

        RuleFor(x => x.DeliveryType)
            .InclusiveBetween((short)1, (short)3).WithMessage("نوع تحویل نامعتبر است");

        RuleFor(x => x.SalesModel)
            .InclusiveBetween((short)1, (short)2).WithMessage("مدل فروش نامعتبر است");

        RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("دسته‌بندی الزامی است");

        RuleFor(x => x.CategoryCode)
            .NotEmpty().WithMessage("کد دسته‌بندی الزامی است")
            .Must(c => c.StartsWith("store.")).WithMessage("کد دسته‌بندی باید با 'store.' شروع شود");

        RuleFor(x => x.StockCount)
            .GreaterThanOrEqualTo(0).WithMessage("تعداد موجودی نمی‌تواند منفی باشد");
    }
}
