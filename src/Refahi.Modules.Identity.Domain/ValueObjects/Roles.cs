namespace Refahi.Modules.Identity.Domain.ValueObjects;

public static class Roles
{
    public const string User = "User";
    public const string Admin = "Admin";
    public const string Vendor = "Vendor";
    public const string ProviderStaff = "ProviderStaff";
    public const string Supervisor = "Supervisor";

    public static readonly string[] All = { User, Admin, Vendor, ProviderStaff, Supervisor };

    public static readonly string[] ManuallyAssignable = { User, Admin, ProviderStaff, Supervisor };

    public static bool IsValid(string role)
    {
        return All.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsManuallyAssignable(string role)
    {
        return ManuallyAssignable.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
