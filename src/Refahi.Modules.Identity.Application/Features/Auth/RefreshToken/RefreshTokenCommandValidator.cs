using FluentValidation;

namespace Refahi.Modules.Identity.Application.Features.Auth.RefreshToken;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .WithMessage("Refresh token is required")
            .MinimumLength(20)
            .WithMessage("Invalid refresh token format");
    }
}
