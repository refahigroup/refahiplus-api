using MediatR;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Application.Contracts.Providers;
using Refahi.Modules.Commerce.Application.Exceptions;
using Refahi.Modules.Commerce.Domain;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Refahi.Modules.Commerce.Application.Features.Checkout.Prepare;

public sealed class PrepareCommerceCheckoutCommandHandler(ICommerceRepository repository, ICommerceProviderFactory providers,
    ICommerceSecretProtector secrets, IMediator mediator) : IRequestHandler<PrepareCommerceCheckoutCommand, PrepareCommerceCheckoutResponse>
{
    public async Task<PrepareCommerceCheckoutResponse> Handle(PrepareCommerceCheckoutCommand request, CancellationToken ct)
    {
        var contact = await mediator.Send(new GetUserCommerceContactQuery(request.UserId), ct);
        var recipientName = contact?.FullName ?? string.Empty;
        var recipientMobile = contact?.MobileNumber ?? string.Empty;
        ValidateContact(recipientName, recipientMobile);

        var commerceOrder = await repository.GetOrderByIdempotencyAsync(request.UserId, request.IdempotencyKey.Trim(), ct);

        if (commerceOrder is not null)
        {
            var replayCart = await repository.GetCartAsync(request.UserId, ct);

            var replayFingerprint = replayCart is { Items.Count: > 0 }
                ? Fingerprint(request.UserId, recipientName, recipientMobile, replayCart.Items.Select(x => (x.ProviderKey, x.ProductKey, x.OfferKey, x.PurchaseOptionKey, x.Quantity, x.ExpectedUnitPriceMinor)))
                : Fingerprint(request.UserId, recipientName, recipientMobile, commerceOrder.Items.Select(x => (x.ProviderKey, x.ProductKey, x.OfferKey, x.PurchaseOptionKey, x.Quantity, x.UnitPriceMinor)));

            commerceOrder.EnsureFingerprint(replayFingerprint);

            return await Resume(commerceOrder, replayCart, ct);
        }

        var cart = await repository.GetCartAsync(request.UserId, ct);

        if (cart is null || cart.Items.Count == 0)
            throw new CommerceDomainException("سبد خرید خالی است", "CART_EMPTY");

        var fingerprint = Fingerprint(
            request.UserId,
            recipientName,
            recipientMobile,
            cart.Items.Select(x => (x.ProviderKey, x.ProductKey, x.OfferKey, x.PurchaseOptionKey, x.Quantity, x.ExpectedUnitPriceMinor))
         );

        var snapshots = new List<CommerceOrderItemSnapshot>();

        foreach (var item in cart.Items.OrderBy(x => x.Id))
        {
            var quote = await providers.GetRequired(item.ProviderKey)
                                       .QuoteAsync(new(
                                           item.ProductKey,
                                           item.OfferKey,
                                           item.PurchaseOptionKey,
                                           item.Quantity
                                       ), ct);

            if (quote.UnitPriceMinor != item.ExpectedUnitPriceMinor)
                throw new CommercePriceChangedException(quote);

            snapshots.Add(new(
                quote.ProviderKey,
                quote.SellerKey,
                quote.ProductKey,
                quote.OfferKey,
                quote.PurchaseOptionKey,
                $"{quote.ProductTitle} - {quote.OptionTitle}",
                quote.OfferTitle,
                quote.CategoryCode,
                quote.Quantity,
                quote.UnitPriceMinor,
                quote.ProviderPayloadJson
            ));
        }

        commerceOrder = CommerceOrder.Create(
            request.UserId,
            request.IdempotencyKey,
            fingerprint,
            secrets.Protect(recipientName.Trim()),
            secrets.Protect(recipientMobile.Trim()),
            snapshots
        );

        await repository.AddOrderAsync(commerceOrder, ct);

        await repository.SaveChangesAsync(ct);

        return await Resume(commerceOrder, cart, ct);
    }

    private async Task<PrepareCommerceCheckoutResponse> Resume(CommerceOrder value, CommerceCart? cart, CancellationToken ct)
    {
        if (!value.OrderId.HasValue)
        {
            var created = await mediator.Send(new CreateOrderCommand(
                value.UserId,
                "Commerce",
                value.Id,
                value.Items.Select(x => new CreateOrderItemInput(
                    x.Title + " - " + x.OfferTitle,
                    x.UnitPriceMinor,
                    x.Quantity,
                    0,
                    x.Id,
                    x.CategoryCode,
                    ["commerce", $"provider:{x.ProviderKey}"],
                    JsonSerializer.Serialize(new
                    {
                        commerce_order_id = value.Id,
                        commerce_order_item_id = x.Id,
                        x.ProviderKey,
                        x.SellerKey,
                        x.ProductKey,
                        x.OfferKey,
                        x.PurchaseOptionKey
                    })
                )).ToList(),
                $"commerce-order:{value.Id:N}",
                ReferenceType: "CommerceOrder"
            ), ct);

            value.AttachOrder(created.OrderId);
            cart?.Clear();

            await repository.SaveChangesAsync(ct);

            return new(
                value.Id,
                created.OrderId,
                created.OrderNumber,
                created.FinalAmountMinor,
                $"/checkout/orders/{created.OrderId}"
            );
        }

        var existing = await mediator.Send(new Orders.Application.Contracts.Queries.GetOrderByIdQuery(value.OrderId.Value, value.UserId, "User"), ct)
            ?? throw new InvalidOperationException("Order متصل به Commerce یافت نشد");

        return new(value.Id, existing.Id, existing.OrderNumber, existing.FinalAmountMinor, $"/checkout/orders/{existing.Id}");
    }

    private static void ValidateContact(string name, string mobile)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length is < 3 or > 255)
            throw new CommerceDomainException(
                "برای ادامه خرید، نام و نام خانوادگی پروفایل خود را تکمیل کنید",
                "PROFILE_CONTACT_INCOMPLETE");

        var digits = new string(mobile.Where(char.IsDigit).ToArray());

        if (digits.Length is < 10 or > 13)
            throw new CommerceDomainException(
                "برای ادامه خرید، شماره موبایل معتبر در پروفایل لازم است",
                "PROFILE_CONTACT_INCOMPLETE");
    }

    private static string Fingerprint(
        Guid userId,
        string recipientName,
        string recipientMobile,
        IEnumerable<(string ProviderKey, string ProductKey, string OfferKey, string PurchaseOptionKey, int Quantity, long UnitPriceMinor)> items)
    {
        var raw = JsonSerializer.Serialize(new
        {
            UserId = userId,
            name = recipientName.Trim(),
            mobile = recipientMobile.Trim(),
            items = items.OrderBy(x => x.ProviderKey)
                         .ThenBy(x => x.ProductKey)
                         .ThenBy(x => x.OfferKey)
                         .ThenBy(x => x.PurchaseOptionKey)
        });

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }
}
