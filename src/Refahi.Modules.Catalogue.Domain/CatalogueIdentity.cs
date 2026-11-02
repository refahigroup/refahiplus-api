namespace Refahi.Modules.Catalogue.Domain;
public static class CatalogueIdentity
{
 public static bool IsSafePath(string path) => path.StartsWith('/') && !path.StartsWith("//") && !path.Contains('\\') && !path.Any(char.IsControl);
}
