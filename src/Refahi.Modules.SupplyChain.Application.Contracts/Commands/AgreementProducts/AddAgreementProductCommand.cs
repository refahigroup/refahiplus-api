using MediatR;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Commands.AgreementProducts;

public sealed record AddAgreementProductCommand(
    Guid AgreementId,
    string Name,
    string? Description,
    int? CategoryId,
    short ProductType,
    short DeliveryType,
    short SalesModel,
    decimal CommissionPercent
) : IRequest<AddAgreementProductResponse>;

public sealed record AddAgreementProductResponse(Guid ProductId);
