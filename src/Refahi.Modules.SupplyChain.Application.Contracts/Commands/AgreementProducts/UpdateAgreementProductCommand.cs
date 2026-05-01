using MediatR;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Commands.AgreementProducts;

public sealed record UpdateAgreementProductCommand(
    Guid AgreementId,
    Guid ProductId,
    string Name,
    string? Description,
    int? CategoryId,
    short ProductType,
    short DeliveryType,
    short SalesModel,
    decimal CommissionPercent
) : IRequest<Unit>;
