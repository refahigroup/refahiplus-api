using MediatR;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Domain.Enums;
using Refahi.Modules.Charge.Domain.Repositories;

namespace Refahi.Modules.Charge.Application.Features;

public sealed class GetChargeRequestHandler : IRequestHandler<GetChargeRequestQuery, ChargeRequestDetailDto?>
{
    private readonly IChargeRequestRepository _requests;

    public GetChargeRequestHandler(IChargeRequestRepository requests)
    {
        _requests = requests;
    }

    public async Task<ChargeRequestDetailDto?> Handle(GetChargeRequestQuery query, CancellationToken ct)
    {
        var request = query.IsAdmin
            ? await _requests.GetAsync(query.RequestId, ct)
            : await _requests.GetForUserAsync(query.RequestId, query.UserId, ct);

        if (request is null)
            return null;

        return new(
            request.Id,
            request.OrderId,
            request.Status.ToString(),
            request.Operator, 
            request.ServiceType,
            request.DestinationMobileNumber, 
            request.ProductCaption, 
            request.ProviderCostMinor, 
            request.MarkupAmountMinor,
            request.FinalAmountMinor, 
            request.ProviderRrn, request.ProviderTraceId, 
            query.IsAdmin ? request.ProviderMessage : UserMessage(request.Status),
            request.CreatedAt, 
            request.FulfilledAt, 
            request.Pins.Count
        );
    }

    private static string? UserMessage(ChargeRequestStatus status) => status switch
    {
        ChargeRequestStatus.Fulfilled => "خرید با موفقیت انجام شد",
        ChargeRequestStatus.Paid or ChargeRequestStatus.Processing or ChargeRequestStatus.ReconciliationPending => "تراکنش در حال پردازش و بررسی است",
        ChargeRequestStatus.Refunding => "بازگشت وجه در حال انجام است",
        ChargeRequestStatus.Refunded => "وجه پرداختی بازگردانده شد",
        ChargeRequestStatus.ManualReview => "تراکنش توسط پشتیبانی در حال بررسی است",
        ChargeRequestStatus.Failed => "خرید ناموفق بود",
        ChargeRequestStatus.Expired => "مهلت درخواست به پایان رسیده است",
        ChargeRequestStatus.Cancelled => "درخواست لغو شده است",
        _ => null
    };
}
