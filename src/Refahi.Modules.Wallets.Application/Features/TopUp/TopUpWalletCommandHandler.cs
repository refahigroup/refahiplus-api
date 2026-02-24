using FluentValidation;
using MediatR;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Features.TopUp;
using Refahi.Modules.Wallets.Application.Contracts.Usecases;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Application.Features.TopUp;

/// <summary>
/// Application handler for TopUp.
///
/// This handler contains no persistence logic. It validates input and delegates
/// the transactional money operation to infrastructure (explicit SQL).
/// </summary>
public sealed class TopUpWalletCommandHandler : IRequestHandler<TopUpWalletCommand, CommandResponse<TopUpWalletResponse>>
{
    private readonly IWalletTopUpUsecase _ops;
    private readonly IValidator<TopUpWalletCommand> _validator;

    public TopUpWalletCommandHandler(IWalletTopUpUsecase ops, IValidator<TopUpWalletCommand> validator)
    {
        _ops = ops;
        _validator = validator;
    }

    public async Task<CommandResponse<TopUpWalletResponse>> Handle(TopUpWalletCommand command, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        return await _ops.TopUpAsync(command, ct);
    }
}
