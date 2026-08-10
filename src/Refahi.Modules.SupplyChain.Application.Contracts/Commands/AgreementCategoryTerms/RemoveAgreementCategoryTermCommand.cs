using MediatR;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Commands.AgreementCategoryTerms;

public sealed record RemoveAgreementCategoryTermCommand(Guid AgreementId, Guid TermId)
    : IRequest<Unit>;
