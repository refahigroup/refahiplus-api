namespace Refahi.Modules.Identity.Domain.ValueObjects;

public static class Roles
{
    public const string User = "User";
    public const string Admin = "Admin";
    public const string Provider = "Provider";
    public const string ProviderStaff = "ProviderStaff";
    public const string Supervisor = "Supervisor";

    public static readonly string[] All =
    {
        User,
        Admin,
        Provider,
        ProviderStaff,
        Supervisor
    };

    public static bool IsValid(string role)
    {
        return All.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
