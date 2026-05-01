namespace Refahi.Modules.Store.Application.Services;

/// <summary>
/// Resolves the set of AgreementProduct IDs that are displayable on a given StoreModule.
/// Result is cached per-module for 2 minutes (IMemoryCache).
/// </summary>
public interface IStoreModuleCatalogService
{
    /// <summary>
    /// Returns all AgreementProduct IDs whose Category is within the module's category subtree,
    /// whose Agreement is approved and not expired, and whose Supplier is approved.
    /// Returns an empty list if the module does not exist, is inactive, or has no CategoryId.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetDisplayableAgreementProductIdsAsync(
        int moduleId, CancellationToken ct = default);
}
