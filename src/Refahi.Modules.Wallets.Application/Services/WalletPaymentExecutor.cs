using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Features.CapturePaymentIntent;
using Refahi.Modules.Wallets.Application.Contracts.Features.CreatePaymentIntent;
using Refahi.Modules.Wallets.Application.Contracts.Features.RefundPayment;
using Refahi.Modules.Wallets.Application.Contracts.Features.ReleasePaymentIntent;
using Refahi.Modules.Wallets.Application.Contracts.Usecases;

namespace Refahi.Modules.Wallets.Application.Services;

public sealed class WalletPaymentExecutor(ISender sender) : IWalletPaymentExecutor
{
    public Task<CommandResponse<CreatePaymentIntentResponse>> ReserveAsync(
        CreatePaymentIntentCommand command,
        CancellationToken ct
    )
    {
        if (!command.PayerOwnerId.HasValue || command.PayerOwnerId == Guid.Empty)
            throw new ArgumentException(
                "هویت مالک پرداخت‌کننده برای رزرو وجه الزامی است.",
                nameof(command)
            );

        return sender.Send(command, ct);
    }

    public Task<CommandResponse<CapturePaymentIntentResponse>> CaptureAsync(
        CapturePaymentIntentCommand command,
        CancellationToken ct
    ) => sender.Send(command, ct);

    public Task<CommandResponse<ReleasePaymentIntentResponse>> ReleaseAsync(
        ReleasePaymentIntentCommand command,
        CancellationToken ct
    ) => sender.Send(command, ct);

    public Task<CommandResponse<RefundPaymentResponse>> RefundAsync(
        RefundPaymentCommand command,
        CancellationToken ct
    ) => sender.Send(command, ct);
}
