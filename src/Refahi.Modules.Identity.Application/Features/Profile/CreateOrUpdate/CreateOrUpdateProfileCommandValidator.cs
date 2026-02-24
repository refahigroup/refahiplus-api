using System.Linq;
using FluentValidation;

namespace Refahi.Modules.Identity.Application.Features.Profile.CreateOrUpdate;

public class CreateOrUpdateProfileCommandValidator : AbstractValidator<CreateOrUpdateProfileCommand>
{
    public CreateOrUpdateProfileCommandValidator()
    {
        // UserId validation
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        // First name validation
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithMessage("First name is required")
            .MaximumLength(100)
            .WithMessage("First name must not exceed 100 characters")
            .Matches(@"^[\u0600-\u06FFa-zA-Z\s]+$")
            .WithMessage("First name must contain only letters and spaces (Persian or English)");

        // Last name validation
        RuleFor(x => x.LastName)
            .NotEmpty()
            .WithMessage("Last name is required")
            .MaximumLength(100)
            .WithMessage("Last name must not exceed 100 characters")
            .Matches(@"^[\u0600-\u06FFa-zA-Z\s]+$")
            .WithMessage("Last name must contain only letters and spaces (Persian or English)");

        // National code validation (Iranian 10-digit format)
        When(x => !string.IsNullOrWhiteSpace(x.NationalCode), () =>
        {
            RuleFor(x => x.NationalCode)
                .Matches(@"^\d{10}$")
                .WithMessage("National code must be exactly 10 digits")
                .Must(BeValidNationalCode)
                .WithMessage("Invalid Iranian national code");
        });

        // Gender validation
        When(x => x.Gender.HasValue, () =>
        {
            RuleFor(x => x.Gender)
                .IsInEnum()
                .WithMessage("Invalid gender value");
        });
    }

    private bool BeValidNationalCode(string? nationalCode)
    {
        if (string.IsNullOrWhiteSpace(nationalCode) || nationalCode.Length != 10)
            return false;

        // Check if all digits are the same (invalid)
        if (nationalCode.Distinct().Count() == 1)
            return false;

        // Calculate check digit
        var check = int.Parse(nationalCode[9].ToString());
        var sum = 0;
        for (int i = 0; i < 9; i++)
        {
            sum += int.Parse(nationalCode[i].ToString()) * (10 - i);
        }
        var remainder = sum % 11;

        return (remainder < 2 && check == remainder) || (remainder >= 2 && check == 11 - remainder);
    }
}
