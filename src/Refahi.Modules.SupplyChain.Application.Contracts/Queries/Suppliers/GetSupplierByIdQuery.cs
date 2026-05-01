using MediatR;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Queries.Suppliers;

public sealed record GetSupplierByIdQuery(Guid Id) : IRequest<SupplierDto?>;
