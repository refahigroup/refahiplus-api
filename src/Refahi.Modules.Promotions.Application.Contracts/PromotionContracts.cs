using MediatR;
namespace Refahi.Modules.Promotions.Application.Contracts;

public sealed record CampaignRules(string Title, string Kind, long FixedAmountMinor, decimal Percent,
    long MaximumDiscountMinor, long MinimumEligibleMinor, long BudgetMinor, int MaximumUses,
    int MaximumUsesPerUser, string[] SourceModules, string[] CategoryPrefixes, Guid[] OrganizationIds,
    Guid[] UserIds, DateTimeOffset StartsAt, DateTimeOffset EndsAt);
public sealed record CampaignDto(Guid Id, string Status, long Version, CampaignRules Rules,
    long OccupiedBudgetMinor, int OccupiedUses,long ReservedBudgetMinor = 0,long ConsumedBudgetMinor = 0);
public sealed record CodeDto(Guid Id, string Mask, Guid? UserId, bool IsActive, int MaximumUses, string BatchKey);
public sealed record CodeExport(Guid Id, string Code, Guid? UserId);
public sealed record PromotionItemInput(Guid OrderItemId, string CategoryCode, long AmountMinor);
public sealed record PromotionItemDiscount(Guid OrderItemId, long AmountMinor);
public sealed record PromotionContext(Guid OrderId, Guid UserId, string SourceModule, long AmountBeforePromotionMinor,
    IReadOnlyList<PromotionItemInput> Items, DateTimeOffset? PayableUntil);
public sealed record PromotionQuote(Guid CampaignId, Guid CodeId, long RuleVersion, long DiscountMinor,
    Guid? OrganizationId, IReadOnlyList<PromotionItemDiscount> Items, DateTimeOffset ExpiresAt);
public sealed record PromotionReservation(Guid Id, Guid OrderId, Guid UserId, string Status, PromotionQuote Quote);
public sealed record RedemptionDto(Guid Id, Guid CampaignId, Guid CodeId, Guid OrderId, Guid UserId,
    Guid? OrganizationId, long DiscountMinor, string Status, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);

public sealed record SaveCampaignCommand(Guid? Id, CampaignRules Rules, Guid ActorId) : IRequest<CampaignDto>;
public sealed record SetCampaignActiveCommand(Guid Id, bool Active, Guid ActorId) : IRequest;
public sealed record ListCampaignsQuery(string? Search = null, int Page = 1, int PageSize = 50) : IRequest<List<CampaignDto>>;
public sealed record GetCampaignQuery(Guid Id) : IRequest<CampaignDto>;
public sealed record GenerateCodesCommand(Guid CampaignId, string? PublicCode, Guid? UserId, int Count,
    int MaximumUses, string IdempotencyKey, Guid ActorId) : IRequest<List<CodeDto>>;
public sealed record ListCodesQuery(Guid CampaignId, int Page = 1, int PageSize = 100) : IRequest<List<CodeDto>>;
public sealed record ExportCodesCommand(Guid CampaignId, string BatchKey, Guid ActorId) : IRequest<List<CodeExport>>;
public sealed record DisableCodeCommand(Guid CampaignId, Guid CodeId, Guid ActorId) : IRequest;
public sealed record ListRedemptionsQuery(Guid? OrderId = null, Guid? UserId = null, string? Status = null,
    int Page = 1, int PageSize = 50) : IRequest<List<RedemptionDto>>;
public sealed record PreviewPromotionQuery(string Code, PromotionContext Context) : IRequest<PromotionQuote>;
public sealed record ReservePromotionCommand(string Code, PromotionContext Context, string IdempotencyKey) : IRequest<PromotionReservation>;
public sealed record BeginPromotionPaymentCommand(Guid ReservationId, Guid OrderId, Guid UserId, bool ResumeExistingIntent = false) : IRequest;
public sealed record ReleasePromotionCommand(Guid ReservationId, Guid OrderId, bool Expired = false) : IRequest;
public sealed record CompletePromotionCommand(Guid ReservationId, Guid OrderId, Guid PaymentId) : IRequest;
public sealed record ReversePromotionCommand(Guid ReservationId, Guid OrderId) : IRequest;
public sealed record GetPromotionReservationQuery(Guid Id) : IRequest<PromotionReservation?>;
public sealed record GetPendingPromotionsQuery(int Take = 50) : IRequest<List<PromotionReservation>>;

public sealed class PromotionException(string message, int statusCode = 400) : Exception(message)
{ public int StatusCode { get; } = statusCode; }

public interface IPromotionCodeProtector
{
    string Protect(string code);
    string Unprotect(string ciphertext);
}
