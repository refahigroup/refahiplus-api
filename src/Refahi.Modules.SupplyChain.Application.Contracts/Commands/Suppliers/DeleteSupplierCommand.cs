using MediatR;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Commands.Suppliers;

public sealed record DeleteSupplierCommand(Guid Id) : IRequest<Unit>;
