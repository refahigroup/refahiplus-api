using FluentValidation;

namespace Refahi.Modules.Identity.Application.Features.Profile.GetProfile;

public class GetProfileQueryValidator : AbstractValidator<GetProfileQuery>
{
    public GetProfileQueryValidator()
    {
        // UserId validation
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");
    }
}
