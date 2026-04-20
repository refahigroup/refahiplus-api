using FluentValidation;
using Refahi.Modules.References.Application.Contracts.Commands;

namespace Refahi.Modules.References.Application.Features.Provinces.CreateProvince;

public class CreateProvinceCommandValidator : AbstractValidator<CreateProvinceCommand>
{
    public CreateProvinceCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام استان الزامی است")
            .MaximumLength(200).WithMessage("نام استان نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("اسلاگ الزامی است")
            .MaximumLength(200).WithMessage("اسلاگ نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد")
            .Matches(@"^[a-z0-9-]+$").WithMessage("اسلاگ فقط می‌تواند شامل حروف کوچک انگلیسی، اعداد و خط تیره باشد");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("ترتیب نمایش باید صفر یا بیشتر باشد");
    }
}
