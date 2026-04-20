using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.Categories;

namespace Refahi.Modules.Store.Application.Features.Categories.CreateCategory;

public class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام دسته‌بندی الزامی است")
            .MaximumLength(200).WithMessage("نام دسته‌بندی نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("اسلاگ الزامی است")
            .MaximumLength(200).WithMessage("اسلاگ نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد")
            .Matches(@"^[a-z0-9-]+$").WithMessage("اسلاگ فقط می‌تواند شامل حروف کوچک انگلیسی، اعداد و خط تیره باشد");

        RuleFor(x => x.CategoryCode)
            .NotEmpty().WithMessage("کد دسته‌بندی الزامی است")
            .MaximumLength(100).WithMessage("کد دسته‌بندی نمی‌تواند بیشتر از ۱۰۰ کاراکتر باشد");

        RuleFor(x => x.ModuleId)
            .GreaterThan(0).WithMessage("شناسه ماژول الزامی است");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("ترتیب نمایش نمی‌تواند منفی باشد");
    }
}
