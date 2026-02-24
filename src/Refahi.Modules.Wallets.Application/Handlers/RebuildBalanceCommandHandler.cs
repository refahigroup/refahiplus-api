using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Commands;
using Refahi.Modules.Wallets.Application.Contracts.Responses;
using Refahi.Modules.Wallets.Application.Services;

namespace Refahi.Modules.Wallets.Application.Handlers;

public sealed class RebuildBalanceCommandHandler 
    : IRequestHandler<RebuildBalanceCommand, CommandResponse<RebuildBalanceResponse>>
{
    private readonly BalanceRebuildApplicationService _service;
    private readonly IValidator<RebuildBalanceCommand> _validator;

    public RebuildBalanceCommandHandler(
        BalanceRebuildApplicationService service,
        IValidator<RebuildBalanceCommand> validator)
    {
        _service = service;
        _validator = validator;
    }

    public async Task<CommandResponse<RebuildBalanceResponse>> Handle(
        RebuildBalanceCommand request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            // Return completed with null to signal validation failure
            return new CommandResponse<RebuildBalanceResponse>(
                CommandStatus.Completed, 
                null);
        }

        return await _service.RebuildBalanceAsync(request, cancellationToken);
    }
}
