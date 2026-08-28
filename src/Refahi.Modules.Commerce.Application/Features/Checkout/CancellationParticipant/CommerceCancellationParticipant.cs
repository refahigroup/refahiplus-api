using Refahi.Modules.Commerce.Application.Contracts.Providers;
using Refahi.Modules.Commerce.Application.Contracts.Providers.Exceptions;
using Refahi.Modules.Commerce.Domain;
using Refahi.Modules.Orders.Application.Contracts.Cancellation;

namespace Refahi.Modules.Commerce.Application.Features.Checkout.CancellationParticipant;

public sealed class CommerceCancellationParticipant(ICommerceRepository repository, ICommerceProviderFactory providers)
    : IOrderCancellationParticipant
{
    public string SourceModule => "Commerce";

    public async Task<OrderCancellationPreparation> PrepareAsync(OrderCancellationContext context, CancellationToken ct)
    {
        var value = await repository.GetOrderByOrderIdAsync(context.OrderId, ct);

        if (value is null)
            return new(false, "سفارش Commerce یافت نشد");

        value.BeginCancellation();

        foreach (var fulfillment in value.Fulfillments)
        {
            if (fulfillment.Status == ProviderFulfillmentStatus.ManualReview)
            {
                value.RequireManualReview();

                await repository.SaveChangesAsync(ct);

                return new(false, "نتیجه عملیات تامین‌کننده نیازمند بررسی دستی است");
            }

            if (fulfillment.Status == ProviderFulfillmentStatus.CancellationPending)
            {
                fulfillment.Fail("نتیجه لغو تامین‌کننده نامشخص است", true);
                value.RequireManualReview();

                await repository.SaveChangesAsync(ct);

                return new(false, "نتیجه لغو تامین‌کننده نیازمند بررسی دستی است");
            }

            if (fulfillment.Status == ProviderFulfillmentStatus.Completed)
            {
                if (string.IsNullOrWhiteSpace(fulfillment.ProviderOrderCode))
                    return new(false, "کد سفارش تامین‌کننده موجود نیست");

                var priorAttempts = fulfillment.Attempts.Where(x => x.Operation == "cancel").ToArray();

                if (priorAttempts.Any(x => x.Outcome == ProviderOperationOutcome.Started))
                {
                    fulfillment.Fail("نتیجه لغو قبلی تامین‌کننده نامشخص است", true);
                    value.RequireManualReview();
                    await repository.SaveChangesAsync(ct);

                    return new(false, "نتیجه لغو تامین‌کننده نیازمند بررسی دستی است");
                }


                var adult = fulfillment.Tickets.Count(x => !x.IsChild);
                var child = fulfillment.Tickets.Count(x => x.IsChild);
                var attempt = fulfillment.BeginAttempt("cancel", $"commerce-cancel:{fulfillment.Id:N}:{priorAttempts.Length + 1}", $"{adult}:{child}");

                await repository.SaveChangesAsync(ct);

                try
                {
                    await providers.GetRequired(fulfillment.ProviderKey)
                                   .CancelAsync(new(attempt.IdempotencyKey, fulfillment.ProviderOrderCode, adult, child), ct);

                    attempt.Complete();
                    fulfillment.MarkCancelled();

                    await repository.SaveChangesAsync(ct);
                }
                catch (CommerceProviderAmbiguousException)
                {
                    attempt.Fail("نتیجه لغو تامین‌کننده نامشخص است", true);
                    fulfillment.Fail("نتیجه لغو تامین‌کننده نامشخص است", true);

                    value.RequireManualReview();

                    await repository.SaveChangesAsync(ct);

                    return new(false, "نتیجه لغو بلیط‌ها نیازمند بررسی دستی است");
                }
                catch (Exception ex)
                {
                    attempt.Fail(ex.Message, false);
                    fulfillment.Fail("لغو تامین‌کننده تکمیل نشد", false);

                    value.BeginCompensation();

                    await repository.SaveChangesAsync(ct);

                    return new(false, "لغو بلیط تامین‌کننده تکمیل نشد");
                }
            }
            else if (fulfillment.Status == ProviderFulfillmentStatus.Failed && fulfillment.Attempts.Any(x => x.Operation == "cancel"))
            {
                value.BeginCompensation();

                await repository.SaveChangesAsync(ct);

                return new(false, "لغو بلیط تامین‌کننده تکمیل نشده است");
            }
            else if (fulfillment.Status is ProviderFulfillmentStatus.Pending or ProviderFulfillmentStatus.Failed)
            {
                fulfillment.MarkCancelled();
            }
        }
        await repository.SaveChangesAsync(ct); return new(true);
    }
}
