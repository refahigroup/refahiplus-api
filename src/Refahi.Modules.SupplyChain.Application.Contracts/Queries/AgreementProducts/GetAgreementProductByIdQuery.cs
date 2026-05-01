using MediatR;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;

public sealed record GetAgreementProductByIdQuery(Guid ProductId) : IRequest<AgreementProductDto?>;
