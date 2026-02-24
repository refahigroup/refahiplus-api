using FluentValidation;
using MediatR;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Features.RefundPayment;
using Refahi.Modules.Wallets.Application.Services;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Application.Features.RefundPayment;

/// <summary>
/// MediatR handler for RefundPayment command.
/// Validates and delegates to application service.
/// </summary>
public sealed class RefundPaymentCommandHandler : IRequestHandler<RefundPaymentCommand, CommandResponse<RefundPaymentResponse>>
{
    private readonly RefundPaymentApplicationService _service;
    private readonly IValidator<RefundPaymentCommand> _validator;

    public RefundPaymentCommandHandler(
        RefundPaymentApplicationService service,
        IValidator<RefundPaymentCommand> validator)
    {
        _service = service;
        _validator = validator;
    }

    public async Task<CommandResponse<RefundPaymentResponse>> Handle(
        RefundPaymentCommand command,
        CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        return await _service.RefundPaymentAsync(command, ct);
    }
}
