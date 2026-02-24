using FluentValidation;
using MediatR;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Features.CapturePaymentIntent;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Application.Features.CapturePaymentIntent;

/// <summary>
/// MediatR handler for CapturePaymentIntent command.
/// Validates and delegates to application service.
/// </summary>
public sealed class CapturePaymentIntentCommandHandler : IRequestHandler<CapturePaymentIntentCommand, CommandResponse<CapturePaymentIntentResponse>>
{
    private readonly Services.CapturePaymentIntentApplicationService _service;
    private readonly IValidator<CapturePaymentIntentCommand> _validator;

    public CapturePaymentIntentCommandHandler(
        Services.CapturePaymentIntentApplicationService service,
        IValidator<CapturePaymentIntentCommand> validator)
    {
        _service = service;
        _validator = validator;
    }

    public async Task<CommandResponse<CapturePaymentIntentResponse>> Handle(
        CapturePaymentIntentCommand command,
        CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        return await _service.CaptureIntentAsync(command, ct);
    }
}
