using FluentValidation;
using MediatR;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Features.ReleasePaymentIntent;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Application.Features.ReleasePaymentIntent;

/// <summary>
/// MediatR handler for ReleasePaymentIntent command.
/// Validates and delegates to application service.
/// </summary>
public sealed class ReleasePaymentIntentCommandHandler : IRequestHandler<ReleasePaymentIntentCommand, CommandResponse<ReleasePaymentIntentResponse>>
{
    private readonly Services.ReleasePaymentIntentApplicationService _service;
    private readonly IValidator<ReleasePaymentIntentCommand> _validator;

    public ReleasePaymentIntentCommandHandler(
        Services.ReleasePaymentIntentApplicationService service,
        IValidator<ReleasePaymentIntentCommand> validator)
    {
        _service = service;
        _validator = validator;
    }

    public async Task<CommandResponse<ReleasePaymentIntentResponse>> Handle(
        ReleasePaymentIntentCommand command,
        CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        return await _service.ReleaseIntentAsync(command, ct);
    }
}
