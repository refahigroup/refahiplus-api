namespace Refahi.Modules.Store.Application.Features.VendorAccess;

internal sealed record ParsedStoreGrant(string ResourceType, Guid ResourceId, string Role);

internal static class StoreGrantCodec
{
    public const string Issuer = "Store";
    public const string EmittedRole = "Vendor";

    public static string Encode(string resourceType, Guid resourceId, string role)
    {
        Validate(resourceType, role);
        return $"v1:{resourceType.ToLowerInvariant()}:{resourceId:N}:{role}";
    }

    public static bool TryParse(string value, out ParsedStoreGrant? grant)
    {
        grant = null;
        var parts = value.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length != 4 || parts[0] != "v1" ||
            !Guid.TryParseExact(parts[2], "N", out var resourceId))
            return false;
        try
        {
            Validate(parts[1], parts[3]);
            grant = new ParsedStoreGrant(parts[1].ToLowerInvariant(), resourceId, parts[3]);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void Validate(string resourceType, string role)
    {
        var valid = resourceType.ToLowerInvariant() switch
        {
            "vendor" => role is "VendorOwner" or "VendorSupervisor",
            "shop" => role is "ShopSupervisor" or "ShopCashier",
            _ => false
        };
        if (!valid) throw new ArgumentException("ترکیب منبع و نقش نامعتبر است");
    }
}
