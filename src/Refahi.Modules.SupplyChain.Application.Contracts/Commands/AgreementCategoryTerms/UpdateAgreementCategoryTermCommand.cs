using MediatR;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Commands.AgreementCategoryTerms;

public sealed record UpdateAgreementCategoryTermCommand(
    Guid AgreementId,
    Guid TermId,
    int CategoryId,
    short AllowedSalesChannels,
    decimal CommissionPercent
) : IRequest<Unit>;
