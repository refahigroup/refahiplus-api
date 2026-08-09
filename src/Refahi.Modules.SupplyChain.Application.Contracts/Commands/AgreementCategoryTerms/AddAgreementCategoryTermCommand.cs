using MediatR;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Commands.AgreementCategoryTerms;

public sealed record AddAgreementCategoryTermCommand(
    Guid AgreementId,
    int CategoryId,
    short AllowedSalesChannels,
    decimal CommissionPercent) : IRequest<AddAgreementCategoryTermResponse>;

public sealed record AddAgreementCategoryTermResponse(Guid TermId);
