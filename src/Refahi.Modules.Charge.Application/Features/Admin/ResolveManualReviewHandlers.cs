using FluentValidation;
using MediatR;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Services;
using Refahi.Modules.Charge.Domain.Repositories;
using Refahi.Modules.Orders.Application.Contracts.Commands;

namespace Refahi.Modules.Charge.Application.Features.Admin;

public sealed class ConfirmChargeFulfilledValidator : AbstractValidator<ConfirmChargeFulfilledCommand>
{
    public ConfirmChargeFulfilledValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty().WithMessage("شناسه درخواست شارژ الزامی است");
        RuleFor(x => x.ProviderRrn).NotEmpty().MaximumLength(100).WithMessage("شماره مرجع تامین‌کننده الزامی است");
        RuleFor(x => x.ProviderTraceId).NotEmpty().MaximumLength(150).WithMessage("شناسه پیگیری تامین‌کننده الزامی است");
        RuleFor(x => x.Evidence).NotEmpty().MaximumLength(1000).WithMessage("مستند تایید الزامی است");
    }
}

public sealed class RefundChargeRequestValidator : AbstractValidator<RefundChargeRequestCommand>
{
    public RefundChargeRequestValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty().WithMessage("شناسه درخواست شارژ الزامی است");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000).WithMessage("دلیل بازگشت وجه الزامی است");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(200).WithMessage("کلید تکرارپذیری الزامی است");
    }
}

public sealed class ConfirmChargeFulfilledHandler : IRequestHandler<ConfirmChargeFulfilledCommand, ReconcileChargeRequestResponse>
{
    private readonly IChargeRequestRepository _requests;
    private readonly ISender _sender;
    public ConfirmChargeFulfilledHandler(IChargeRequestRepository requests, ISender sender)
    { _requests = requests; _sender = sender; }

    public async Task<ReconcileChargeRequestResponse> Handle(ConfirmChargeFulfilledCommand command, CancellationToken ct)
    {
        var request = await _requests.GetAsync(command.RequestId, ct) ?? throw new ArgumentException("درخواست شارژ یافت نشد");
        request.ConfirmFulfilledByAdmin(command.ProviderRrn, command.ProviderTraceId, command.Evidence, DateTime.UtcNow);
        await _requests.SaveChangesAsync(ct);
        if (request.OrderId.HasValue)
        {
            await _sender.Send(new UpdateOrderStatusCommand(request.OrderId.Value, OrderStatusInput.Processing), ct);
            await _sender.Send(new UpdateOrderStatusCommand(request.OrderId.Value, OrderStatusInput.Delivered), ct);
        }
        return Map(request);
    }

    private static ReconcileChargeRequestResponse Map(Domain.Aggregates.ChargeRequest request) =>
        new(request.Id, request.Status.ToString(), request.Attempts.Count, request.NextReconciliationAt,
            request.ProviderRrn, request.ProviderTraceId, request.ProviderMessage);
}

public sealed class RefundChargeRequestHandler : IRequestHandler<RefundChargeRequestCommand, ReconcileChargeRequestResponse>
{
    private readonly IChargeRequestRepository _requests;
    private readonly ChargeRefundProcessor _refunds;
    public RefundChargeRequestHandler(IChargeRequestRepository requests, ChargeRefundProcessor refunds)
    { _requests = requests; _refunds = refunds; }

    public async Task<ReconcileChargeRequestResponse> Handle(RefundChargeRequestCommand command, CancellationToken ct)
    {
        var request = await _requests.GetAsync(command.RequestId, ct) ?? throw new ArgumentException("درخواست شارژ یافت نشد");
        if (!request.OrderId.HasValue || !request.PaymentId.HasValue)
            throw new InvalidOperationException("پرداخت قابل بازگشت برای درخواست یافت نشد");
        await _refunds.BeginAsync(request, command.Reason, command.IdempotencyKey, ct);
        return new(request.Id, request.Status.ToString(), request.Attempts.Count, request.NextReconciliationAt,
            request.ProviderRrn, request.ProviderTraceId, request.ProviderMessage);
    }
}
