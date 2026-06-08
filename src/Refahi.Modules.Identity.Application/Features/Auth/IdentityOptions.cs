namespace Refahi.Modules.Identity.Application.Features.Auth;

public sealed class IdentityOptions
{
    public const string SectionName = "Identity";

    public bool AutoRegistrationEnabled { get; init; } = true;

    public bool QuickRegistrationEnabled { get; init; } = true;
}
