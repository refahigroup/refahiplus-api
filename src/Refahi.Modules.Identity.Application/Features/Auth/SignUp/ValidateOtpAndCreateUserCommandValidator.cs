using FluentValidation;

namespace Refahi.Modules.Identity.Application.Features.Auth.SignUp;

public class ValidateOtpAndCreateUserCommandValidator : AbstractValidator<ValidateOtpAndCreateUserCommand>
{
    public ValidateOtpAndCreateUserCommandValidator()
    {
        // Token validation
        RuleFor(x => x.Token)
            .NotEmpty()
            .WithMessage("OTP token is required");

        // OTP code validation (6 digits)
        RuleFor(x => x.OtpCode)
            .NotEmpty()
            .WithMessage("OTP code is required")
            .Matches(@"^\d{6}$")
            .WithMessage("OTP code must be exactly 6 digits");
    }
}
