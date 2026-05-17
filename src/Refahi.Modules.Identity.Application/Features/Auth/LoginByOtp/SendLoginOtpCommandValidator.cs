using FluentValidation;

namespace Refahi.Modules.Identity.Application.Features.Auth.LoginByOtp;

public class SendLoginOtpCommandValidator : AbstractValidator<SendLoginOtpCommand>
{
    public SendLoginOtpCommandValidator()
    {
        RuleFor(x => x.Contact)
            .NotEmpty()
            .WithMessage("شماره موبایل یا ایمیل الزامی است")
            .Must(BeValidMobileOrEmail)
            .WithMessage("شماره موبایل یا ایمیل معتبر وارد کنید");
    }

    private static bool BeValidMobileOrEmail(string? contact)
    {
        if (string.IsNullOrWhiteSpace(contact)) return false;
        return System.Text.RegularExpressions.Regex.IsMatch(contact, @"^09\d{9}$") ||
               new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(contact);
    }
}
