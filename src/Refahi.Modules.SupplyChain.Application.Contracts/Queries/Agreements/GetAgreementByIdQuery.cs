using MediatR;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Queries.Agreements;

public sealed record GetAgreementByIdQuery(Guid Id) : IRequest<AgreementDto?>;
