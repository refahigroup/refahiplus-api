using MediatR;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Commands.Suppliers;

public sealed record ChangeSupplierStatusCommand(
    Guid Id,
    short NewStatus,
    string? Note
) : IRequest<Unit>;
