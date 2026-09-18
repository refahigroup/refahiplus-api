using MediatR;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;

namespace Refahi.Modules.CreditPayments.Application;

public sealed class ResendWebhookEventHandler(IWebhookOperationsRepository repository, TimeProvider clock)
    : IRequestHandler<ResendWebhookEventCommand, ResendWebhookEventResponse>
{
    public async Task<ResendWebhookEventResponse> Handle(
        ResendWebhookEventCommand request, CancellationToken ct)
    {
        if (request.EventId == Guid.Empty)
            throw new CreditPaymentsApplicationException("webhook_event_invalid", "شناسه رویداد وب‌هوک نامعتبر است");
        if (request.Reason?.Trim().Length > 250)
            throw new CreditPaymentsApplicationException("webhook_resend_reason_invalid", "دلیل ارسال مجدد حداکثر ۲۵۰ نویسه است");
        if (!await repository.ResendDeadLetterAsync(request.EventId, request.ActorId,
                request.Reason?.Trim(), clock.GetUtcNow(), ct))
            throw new CreditPaymentsApplicationException("webhook_delivery_not_dead_lettered",
                "رویداد وب‌هوک برای ارسال مجدد در وضعیت مجاز نیست", 409);
        return new(request.EventId, "pending");
    }
}
