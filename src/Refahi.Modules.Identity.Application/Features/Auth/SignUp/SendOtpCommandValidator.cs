using FluentValidation;

namespace Refahi.Modules.Identity.Application.Features.Auth.SignUp;

public class SendOtpCommandValidator : AbstractValidator<SendOtpCommand>
{
    public SendOtpCommandValidator()
    {
        // At least one contact method must be provided
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.MobileNumber) || !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Either MobileNumber or Email must be provided")
            .WithName("ContactInfo");

        // Mobile number validation (Iranian format: 09xxxxxxxxx)
        When(x => !string.IsNullOrWhiteSpace(x.MobileNumber), () =>
        {
            RuleFor(x => x.MobileNumber)
                .NotEmpty()
                .WithMessage("Mobile number is required when provided")
                .Matches(@"^09\d{9}$")
                .WithMessage("Mobile number must be in format 09xxxxxxxxx (11 digits)")
                .MaximumLength(11)
                .WithMessage("Mobile number must be exactly 11 digits");
        });

        // Email validation
        When(x => !string.IsNullOrWhiteSpace(x.Email), () =>
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .WithMessage("Email is required when provided")
                .EmailAddress()
                .WithMessage("Invalid email format")
                .MaximumLength(255)
                .WithMessage("Email must not exceed 255 characters");
        });
    }
}
