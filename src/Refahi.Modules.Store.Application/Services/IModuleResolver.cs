namespace Refahi.Modules.Store.Application.Services;

public interface IModuleResolver
{
    /// <summary>Resolves an active module by slug. Returns null if not found or inactive.</summary>
    Task<int?> ResolveIdAsync(string slug, CancellationToken ct = default);
}
