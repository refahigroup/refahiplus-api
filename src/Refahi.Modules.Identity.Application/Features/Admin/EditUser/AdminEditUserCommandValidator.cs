using FluentValidation;

namespace Refahi.Modules.Identity.Application.Features.Admin.EditUser;

public class AdminEditUserCommandValidator : AbstractValidator<AdminEditUserCommand>
{
    public AdminEditUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("شناسه کاربر الزامی است");
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100).WithMessage("نام الزامی است");
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100).WithMessage("نام خانوادگی الزامی است");
        RuleFor(x => x.NationalCode)
            .Length(10).WithMessage("کد ملی باید ۱۰ رقم باشد")
            .When(x => !string.IsNullOrWhiteSpace(x.NationalCode));
    }
}
