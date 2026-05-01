using MediatR;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Commands.Agreements;

public sealed record ChangeAgreementStatusCommand(
    Guid Id,
    short NewStatus,
    string? Note
) : IRequest<Unit>;
