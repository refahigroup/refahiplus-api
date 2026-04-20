using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.Modules;

namespace Refahi.Modules.Store.Application.Features.Modules.UpdateModule;

public class UpdateModuleCommandValidator : AbstractValidator<UpdateModuleCommand>
{
    public UpdateModuleCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("شناسه ماژول نامعتبر است");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام ماژول الزامی است")
            .MaximumLength(200).WithMessage("نام ماژول نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("ترتیب نمایش نمی‌تواند منفی باشد");
    }
}
