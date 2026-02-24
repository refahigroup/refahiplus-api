using FluentValidation;
using MediatR;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Features.CreatePaymentIntent;
using Refahi.Modules.Wallets.Application.Services;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Application.Features.CreatePaymentIntent;

/// <summary>
/// MediatR handler for CreatePaymentIntent command.
/// Validates and delegates to application service.
/// </summary>
public sealed class CreatePaymentIntentCommandHandler : IRequestHandler<CreatePaymentIntentCommand, CommandResponse<CreatePaymentIntentResponse>>
{
    private readonly CreatePaymentIntentApplicationService _service;
    private readonly IValidator<CreatePaymentIntentCommand> _validator;

    public CreatePaymentIntentCommandHandler(
        CreatePaymentIntentApplicationService service,
        IValidator<CreatePaymentIntentCommand> validator)
    {
        _service = service;
        _validator = validator;
    }

    public async Task<CommandResponse<CreatePaymentIntentResponse>> Handle(
        CreatePaymentIntentCommand command,
        CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        return await _service.CreateIntentAsync(command, ct);
    }
}
