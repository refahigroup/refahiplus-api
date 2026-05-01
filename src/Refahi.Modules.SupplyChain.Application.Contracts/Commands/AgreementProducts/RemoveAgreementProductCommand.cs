using MediatR;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Commands.AgreementProducts;

public sealed record RemoveAgreementProductCommand(
    Guid AgreementId,
    Guid ProductId
) : IRequest<Unit>;
