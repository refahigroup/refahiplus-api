using MediatR;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Commands.Agreements;

public sealed record DeleteAgreementCommand(Guid Id) : IRequest<Unit>;
