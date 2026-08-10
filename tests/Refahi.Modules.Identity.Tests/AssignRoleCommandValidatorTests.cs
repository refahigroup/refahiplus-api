using Refahi.Modules.Identity.Application.Features.Roles.AssignRole;
using Xunit;

namespace Refahi.Modules.Identity.Tests;

public sealed class AssignRoleCommandValidatorTests
{
    private readonly AssignRoleCommandValidator _validator = new();

    [Fact]
    public void Rejects_manual_vendor_role_assignment()
    {
        var result = _validator.Validate(new AssignRoleCommand(Guid.NewGuid(), "Vendor"));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(AssignRoleCommand.Role)
        );
    }

    [Theory]
    [InlineData("User")]
    [InlineData("Admin")]
    public void Accepts_manually_assignable_roles(string role)
    {
        var result = _validator.Validate(new AssignRoleCommand(Guid.NewGuid(), role));

        Assert.True(result.IsValid);
    }
}
