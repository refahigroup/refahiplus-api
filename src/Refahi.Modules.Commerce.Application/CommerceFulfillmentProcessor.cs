using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Application.Contracts.Providers;
using Refahi.Modules.Commerce.Application.Contracts.Providers.Exceptions;
using Refahi.Modules.Commerce.Domain;
using Refahi.Modules.Orders.Application.Contracts.Commands;

namespace Refahi.Modules.Commerce.Application;

public sealed class CommerceFulfillmentProcessor(ICommerceRepository repository, ICommerceProviderFactory providers,
    ICommerceSecretProtector secrets, IMediator mediator, ILogger<CommerceFulfillmentProcessor> logger,
    IOptions<CommerceRuntimeOptions> runtimeOptions)
{
    public async Task ProcessBatchAsync(CancellationToken ct)
    {
        foreach (var order in await repository.GetPendingFulfillmentAsync(10, ct))
        {
            using var scope = logger.BeginScope(new Dictionary<string, object?> 
            { 
                ["CommerceOrderId"] = order.Id, 
                ["OrderId"] = order.OrderId, 
                ["PaymentId"] = order.PaymentId 
            });

            await ProcessAsync(order, ct);
        }

        var cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Clamp(runtimeOptions.Value.RecipientRetentionDays, 1, 3650));
        var expired = await repository.GetOrdersForRecipientRedactionAsync(cutoff, 100, ct);

        foreach (var order in expired) 
            order.RedactRecipient();

        if (expired.Count > 0) 
            await repository.SaveChangesAsync(ct);

    }

    private async Task ProcessAsync(CommerceOrder order, CancellationToken ct)
    {
        if (!order.OrderId.HasValue) 
        { 
            order.RequireManualReview(); 
            await repository.SaveChangesAsync(ct); return; 
        }

        if (order.Status == CommerceOrderStatus.CompensationPending) 
        { 
            await CompensateAsync(order, ct); 
            return; 
        }

        order.BeginFulfillment(); 
        
        await repository.SaveChangesAsync(ct);

        foreach (var fulfillment in order.Fulfillments.OrderBy(x => x.ProviderKey))
        {
            if (fulfillment.Status is ProviderFulfillmentStatus.Completed or ProviderFulfillmentStatus.Cancelled) 
                continue;

            if (fulfillment.Status == ProviderFulfillmentStatus.ManualReview)
            { 
                order.RequireManualReview(); 
                await repository.SaveChangesAsync(ct); 

                return; 
            }

            if (fulfillment.Attempts.Any(x => x.Operation == "fulfill" && x.Outcome == ProviderOperationOutcome.Started))
            {
                fulfillment.Fail("نتیجه تلاش قبلی تامین‌کننده نامشخص است", true);
                order.RequireManualReview(); await repository.SaveChangesAsync(ct); 
                
                return; 
            }

            var lines = order
                .Items
                .Where(x => x.ProviderKey.Equals(fulfillment.ProviderKey, StringComparison.OrdinalIgnoreCase))
                .Select(x => new CommerceFulfillmentLine(
                    x.OfferKey, 
                    x.PurchaseOptionKey, 
                    x.Quantity, 
                    x.UnitPriceMinor, 
                    x.ProviderPayloadJson)
                ).ToArray();

            var operationKey = $"commerce-fulfill:{fulfillment.Id:N}";

            var payloadHash = Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        string.Join('|', lines.Select(x => $"{x.OfferKey}:{x.PurchaseOptionKey}:{x.Quantity}:{x.UnitPriceMinor}"))
                    )
                )
            );

            var attempt = fulfillment.BeginAttempt("fulfill", operationKey, payloadHash); 
            
            await repository.SaveChangesAsync(ct);

            try
            {
                var result = await providers.GetRequired(fulfillment.ProviderKey)
                                            .FulfillAsync(new(
                                                operationKey,
                                                secrets.Unprotect(order.RecipientNameProtected), 
                                                secrets.Unprotect(order.RecipientMobileProtected), 
                                                lines
                                            ), ct);

                fulfillment.Complete(
                    result.ProviderOrderCode,
                    result.Tickets.Select(x => (secrets.Hash(x.Code), secrets.Protect(x.Code), x.IsChild))
                );

                attempt.Complete(); 
                
                await repository.SaveChangesAsync(ct);

                logger.LogInformation(
                    "Commerce fulfillment completed. ProviderKey={ProviderKey}, FulfillmentId={FulfillmentId}",
                    fulfillment.ProviderKey,
                    fulfillment.Id
                );
            }
            catch (CommerceProviderAmbiguousException ex)
            { 
                attempt.Fail("نتیجه عملیات تامین‌کننده نامشخص است", true); 
                fulfillment.Fail("نتیجه عملیات تامین‌کننده نامشخص است", true); 

                order.RequireManualReview(); 

                await repository.SaveChangesAsync(ct);

                logger.LogError(ex, "Commerce fulfillment ambiguous. ProviderKey={ProviderKey}, FulfillmentId={FulfillmentId}", fulfillment.ProviderKey, fulfillment.Id); 
                
                return; 
            }
            catch (Exception ex)
            { 
                attempt.Fail("عملیات تامین‌کننده ناموفق بود", false); 
                fulfillment.Fail("عملیات تامین‌کننده ناموفق بود", false); 
                
                order.BeginCompensation(); 
                await repository.SaveChangesAsync(ct);

                logger.LogError(ex, "Commerce fulfillment failed. ProviderKey={ProviderKey}, FulfillmentId={FulfillmentId}", fulfillment.ProviderKey, fulfillment.Id); 
                
                await CompensateAsync(order, ct); 
                
                return; 
            }
        }

        order.Complete(); await repository.SaveChangesAsync(ct);
    }

    private async Task CompensateAsync(CommerceOrder order, CancellationToken ct)
    {
        if (!order.OrderId.HasValue) 
        { 
            order.RequireManualReview(); 
            
            await repository.SaveChangesAsync(ct); 
            
            return; 
        }

        try
        { 
            await mediator.Send(new CancelOrderCommand(
                order.OrderId.Value, 
                "جبران شکست تامین‌کننده Commerce",
                $"commerce-compensation:{order.Id:N}", 
                CallerRole: 
                "System"
            ), ct); 
        }
        catch (Exception ex) 
        { 
            logger.LogError(ex, "Commerce compensation did not complete. CommerceOrderId={CommerceOrderId}", order.Id); 
        }
    }
}

public sealed class CommerceRuntimeOptions
{
    public const string SectionName = "Commerce";
    public int RecipientRetentionDays { get; init; } = 90;
}

public sealed class ProcessCommerceFulfillmentBatchHandler(CommerceFulfillmentProcessor processor)
    : IRequestHandler<ProcessCommerceFulfillmentBatchCommand>
{
    public async Task<Unit> Handle(ProcessCommerceFulfillmentBatchCommand request, CancellationToken ct)
    { 
        await processor.ProcessBatchAsync(ct);
        
        return Unit.Value; 
    }
}
