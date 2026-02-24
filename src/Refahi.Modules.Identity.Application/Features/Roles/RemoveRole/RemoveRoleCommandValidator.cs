using FluentValidation;
using DomainRoles = Refahi.Modules.Identity.Domain.ValueObjects.Roles;

namespace Refahi.Modules.Identity.Application.Features.Roles.RemoveRole;

public class RemoveRoleCommandValidator : AbstractValidator<RemoveRoleCommand>
{
    public RemoveRoleCommandValidator()
    {
        // UserId validation
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        // Role validation
        RuleFor(x => x.Role)
            .NotEmpty()
            .WithMessage("Role is required")
            .MaximumLength(50)
            .WithMessage("Role name must not exceed 50 characters")
            .Must(BeValidRole)
            .WithMessage(x => $"Invalid role '{x.Role}'. Valid roles are: {string.Join(", ", DomainRoles.All)}");
    }

    private bool BeValidRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;

        return DomainRoles.IsValid(role);
    }
}
