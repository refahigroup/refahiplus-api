using FluentValidation;
using Refahi.Modules.Identity.Application.Contracts.Features.Profile.UpdateMe;

namespace Refahi.Modules.Identity.Application.Features.Profile.UpdateMe;

public class UpdateMeCommandValidator : AbstractValidator<UpdateMeCommand>
{
    public UpdateMeCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("نام الزامی است")
            .MinimumLength(2).WithMessage("نام باید حداقل ۲ کاراکتر باشد")
            .MaximumLength(50).WithMessage("نام نباید بیشتر از ۵۰ کاراکتر باشد");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("نام خانوادگی الزامی است")
            .MaximumLength(50).WithMessage("نام خانوادگی نباید بیشتر از ۵۰ کاراکتر باشد");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("فرمت ایمیل نامعتبر است")
            .When(x => !string.IsNullOrEmpty(x.Email));
    }
}
