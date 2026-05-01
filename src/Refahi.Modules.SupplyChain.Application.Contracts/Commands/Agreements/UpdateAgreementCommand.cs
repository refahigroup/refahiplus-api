using MediatR;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Commands.Agreements;

public sealed record UpdateAgreementCommand(
    Guid Id,
    string AgreementNo,
    short Type,
    DateTimeOffset FromDate,
    DateTimeOffset ToDate
) : IRequest<Unit>;
