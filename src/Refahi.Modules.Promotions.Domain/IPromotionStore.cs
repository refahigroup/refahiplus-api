namespace Refahi.Modules.Promotions.Domain;

public interface IPromotionTransaction : IAsyncDisposable { Task CommitAsync(CancellationToken ct); }
public interface IPromotionStore
{
    Task<IPromotionTransaction> LockCampaignAsync(Guid campaignId, CancellationToken ct);
    Task<PromotionCampaign?> CampaignAsync(Guid id, CancellationToken ct);
    Task<PromotionCode?> CodeByHashAsync(string hash, CancellationToken ct);
    Task<PromotionCode?> CodeAsync(Guid id, CancellationToken ct);
    Task<PromotionRedemption?> RedemptionAsync(Guid id, CancellationToken ct);
    Task<PromotionRedemption?> OperationAsync(Guid orderId, string key, CancellationToken ct);
    Task<PromotionUsage> UsageAsync(Guid campaignId, Guid? userId, Guid? codeId, CancellationToken ct);
    Task<List<PromotionCampaign>> CampaignsAsync(string? search, int skip, int take, CancellationToken ct);
    Task<List<PromotionCode>> CodesAsync(Guid campaignId, int skip, int take, CancellationToken ct);
    Task<List<PromotionCode>> BatchCodesAsync(Guid campaignId, string key, bool exact, CancellationToken ct);
    Task<List<PromotionRedemption>> RedemptionsAsync(Guid? orderId, Guid? userId, RedemptionStatus? status, int skip, int take, CancellationToken ct);
    Task<List<PromotionRedemption>> PendingAsync(int take, CancellationToken ct);
    void Add(PromotionCampaign value);
    void Add(PromotionCode value);
    void Add(PromotionRedemption value);
    void Add(PromotionAudit value);
    Task SaveAsync(CancellationToken ct);
}
public sealed record PromotionUsage(long BudgetMinor, int Uses, int UserUses, int CodeUses,long ReservedBudgetMinor = 0,long ConsumedBudgetMinor = 0);
