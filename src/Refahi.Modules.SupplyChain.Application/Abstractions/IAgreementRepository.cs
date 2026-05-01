using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;
using Refahi.Modules.SupplyChain.Domain.Aggregates;
using Refahi.Modules.SupplyChain.Domain.Entities;
using Refahi.Modules.SupplyChain.Domain.Enums;

namespace Refahi.Modules.SupplyChain.Application.Abstractions;

public interface IAgreementRepository
{
    Task<Agreement?> GetByIdAsync(Guid id, bool includeProducts, CancellationToken ct);
    Task<AgreementProduct?> GetProductByIdAsync(Guid productId, CancellationToken ct);
    Task<(IReadOnlyList<Agreement> Items, int Total)> GetPagedAsync(
        Guid? supplierId, AgreementStatus? status, AgreementType? type, string? search,
        int page, int size, CancellationToken ct);
    Task<bool> ExistsByAgreementNoAsync(string agreementNo, Guid? excludeId, CancellationToken ct);
    /// <summary>Returns IDs of AgreementProducts belonging to approved, non-expired agreements in the given category.</summary>
    [Obsolete("Use GetDisplayableProductIdsByCategoriesAsync with a subtree of categoryIds instead.")]
    Task<IReadOnlyList<Guid>> GetApprovedProductIdsByCategoryAsync(int categoryId, CancellationToken ct);
    /// <summary>Returns fully displayable AgreementProduct IDs: CategoryId ∈ categoryIds, Agreement approved+not-expired, Supplier approved.</summary>
    Task<IReadOnlyList<Guid>> GetDisplayableProductIdsByCategoriesAsync(IReadOnlyList<int> categoryIds, CancellationToken ct);
    /// <summary>Batch-fetches AgreementProducts by their IDs, keyed by ID.</summary>
    Task<IReadOnlyDictionary<Guid, AgreementProductDto>> GetProductsByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct);
    /// <summary>Returns CommissionPercent keyed by AgreementProduct ID for the given IDs.</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetCommissionPercentsByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct);
    Task AddAsync(Agreement agreement, CancellationToken ct);
    void Update(Agreement agreement);
    /// <summary>Explicitly registers a newly-created AgreementProduct as Added so EF generates INSERT.</summary>
    void AddProduct(AgreementProduct product);
    Task SaveChangesAsync(CancellationToken ct);
}
