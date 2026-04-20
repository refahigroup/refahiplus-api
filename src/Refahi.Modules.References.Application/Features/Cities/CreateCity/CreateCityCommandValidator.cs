using FluentValidation;
using Refahi.Modules.References.Application.Contracts.Commands;

namespace Refahi.Modules.References.Application.Features.Cities.CreateCity;

public class CreateCityCommandValidator : AbstractValidator<CreateCityCommand>
{
    public CreateCityCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام شهر الزامی است")
            .MaximumLength(200).WithMessage("نام شهر نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("اسلاگ الزامی است")
            .MaximumLength(200).WithMessage("اسلاگ نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد")
            .Matches(@"^[a-z0-9-]+$").WithMessage("اسلاگ فقط می‌تواند شامل حروف کوچک انگلیسی، اعداد و خط تیره باشد");

        RuleFor(x => x.ProvinceId)
            .GreaterThan(0).WithMessage("شناسه استان نامعتبر است");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("ترتیب نمایش باید صفر یا بیشتر باشد");
    }
}
