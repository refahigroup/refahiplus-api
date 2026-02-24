using FluentValidation;

namespace Refahi.Modules.Identity.Application.Features.Auth.SetPassword;

public class SetPasswordCommandValidator : AbstractValidator<SetPasswordCommand>
{
    public SetPasswordCommandValidator()
    {
        // Mobile or Email validation
        RuleFor(x => x.MobileOrEmail)
            .NotEmpty()
            .WithMessage("Mobile number or email is required")
            .Must(BeValidMobileOrEmail)
            .WithMessage("Must be a valid mobile number (09xxxxxxxxx) or email address");

        // Password validation
        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required")
            .MinimumLength(8)
            .WithMessage("Password must be at least 8 characters long")
            .MaximumLength(128)
            .WithMessage("Password must not exceed 128 characters")
            .Matches(@"[A-Z]")
            .WithMessage("Password must contain at least one uppercase letter")
            .Matches(@"[a-z]")
            .WithMessage("Password must contain at least one lowercase letter")
            .Matches(@"\d")
            .WithMessage("Password must contain at least one digit")
            .Matches(@"[^a-zA-Z0-9]")
            .WithMessage("Password must contain at least one special character");
    }

    private bool BeValidMobileOrEmail(string mobileOrEmail)
    {
        if (string.IsNullOrWhiteSpace(mobileOrEmail))
            return false;

        // Check if it's a valid Iranian mobile number
        if (System.Text.RegularExpressions.Regex.IsMatch(mobileOrEmail, @"^09\d{9}$"))
            return true;

        // Check if it's a valid email
        if (mobileOrEmail.Contains("@"))
        {
            var emailRegex = new System.Text.RegularExpressions.Regex(
                @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return emailRegex.IsMatch(mobileOrEmail);
        }

        return false;
    }
}
