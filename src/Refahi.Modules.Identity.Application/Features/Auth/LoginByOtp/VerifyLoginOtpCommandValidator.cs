using FluentValidation;

namespace Refahi.Modules.Identity.Application.Features.Auth.LoginByOtp;

public class VerifyLoginOtpCommandValidator : AbstractValidator<VerifyLoginOtpCommand>
{
    public VerifyLoginOtpCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty()
            .WithMessage("توکن OTP الزامی است");

        RuleFor(x => x.OtpCode)
            .NotEmpty()
            .WithMessage("کد OTP الزامی است")
            .Matches(@"^\d{6}$")
            .WithMessage("کد OTP باید دقیقاً ۶ رقم باشد");

        RuleFor(x => x.Flow)
            .Must(x => Refahi.Modules.Identity.Application.Features.Auth.AuthFlow.IsSignIn(x) ||
                       Refahi.Modules.Identity.Application.Features.Auth.AuthFlow.IsSignUp(x))
            .WithMessage("جریان OTP نامعتبر است");
    }
}
