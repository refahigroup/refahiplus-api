using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.Modules;

namespace Refahi.Modules.Store.Application.Features.Modules.CreateModule;

public class CreateModuleCommandValidator : AbstractValidator<CreateModuleCommand>
{
    public CreateModuleCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام ماژول الزامی است")
            .MaximumLength(200).WithMessage("نام ماژول نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("اسلاگ الزامی است")
            .MaximumLength(100).WithMessage("اسلاگ نمی‌تواند بیشتر از ۱۰۰ کاراکتر باشد")
            .Matches(@"^[a-z0-9-]+$").WithMessage("اسلاگ فقط می‌تواند شامل حروف کوچک انگلیسی، اعداد و خط تیره باشد");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("ترتیب نمایش نمی‌تواند منفی باشد");
    }
}
