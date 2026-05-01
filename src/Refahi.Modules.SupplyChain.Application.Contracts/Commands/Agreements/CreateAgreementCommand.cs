using MediatR;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Commands.Agreements;

public sealed record CreateAgreementCommand(
    string AgreementNo,
    short Type,
    Guid SupplierId,
    DateTimeOffset FromDate,
    DateTimeOffset ToDate
) : IRequest<CreateAgreementResponse>;

public sealed record CreateAgreementResponse(Guid Id);
