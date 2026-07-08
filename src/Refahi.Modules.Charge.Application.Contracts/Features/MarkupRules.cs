using MediatR;
using Refahi.Modules.Charge.Domain.Enums;

namespace Refahi.Modules.Charge.Application.Contracts.Features;

public sealed record UpsertMarkupRuleCommand(Guid? RuleId, ChargeOperator? Operator, ChargeServiceType? ServiceType,
    decimal Percent, long FixedAmountMinor, DateTime EffectiveFrom, DateTime? EffectiveTo) : IRequest<MarkupRuleDto>;
public sealed record DeactivateMarkupRuleCommand(Guid RuleId) : IRequest;
public sealed record GetMarkupRulesQuery : IRequest<IReadOnlyList<MarkupRuleDto>>;
public sealed record MarkupRuleDto(Guid Id, ChargeOperator? Operator, ChargeServiceType? ServiceType, decimal Percent,
    long FixedAmountMinor, DateTime EffectiveFrom, DateTime? EffectiveTo, bool IsActive);

public sealed record GetProviderBalanceQuery : IRequest<Providers.ProviderBalanceDto>;
public sealed record GetProviderErrorsQuery : IRequest<IReadOnlyList<Providers.ProviderErrorDto>>;
public sealed record GetProviderChannelsQuery : IRequest<IReadOnlyList<Providers.ProviderChannelDto>>;
public sealed record GetProviderTransactionReportQuery(int PageNumber, DateOnly? FromDate, DateOnly? ToDate)
    : IRequest<Providers.ProviderReportDto>;
public sealed record GetProviderWalletChargeReportQuery(int PageNumber, DateOnly? FromDate, DateOnly? ToDate)
    : IRequest<Providers.ProviderReportDto>;
