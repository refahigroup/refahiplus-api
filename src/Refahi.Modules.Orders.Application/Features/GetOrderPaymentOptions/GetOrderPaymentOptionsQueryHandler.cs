using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Orders.Domain.Repositories;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetMyWallets;

namespace Refahi.Modules.Orders.Application.Features.GetOrderPaymentOptions;

public sealed class GetOrderPaymentOptionsQueryHandler
    : IRequestHandler<GetOrderPaymentOptionsQuery, OrderPaymentOptionsDto?>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IMediator _mediator;

    public GetOrderPaymentOptionsQueryHandler(IOrderRepository orderRepository, IMediator mediator)
    {
        _orderRepository = orderRepository;
        _mediator = mediator;
    }

    public async Task<OrderPaymentOptionsDto?> Handle(
        GetOrderPaymentOptionsQuery request,
        CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdWithItemsAsync(request.OrderId, cancellationToken);
        if (order is null)
            return null;

        if (request.CallerRole == "User" && order.UserId != request.CallerUserId)
            return null;

        var wallets = await _mediator.Send(new GetMyWalletsQuery(order.UserId), cancellationToken);
        var categoryCodes = order.Items
            .Select(i => i.CategoryCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var now = DateTimeOffset.UtcNow;
        var options = wallets
            .Select(w => BuildWalletOption(w, order.Currency, categoryCodes, now))
            .ToList();

        var priorityWallets = options
            .Where(w => w.IsAllowed && w.AvailableBalanceMinor > 0)
            .OrderBy(w => IsOrgCredit(w.WalletType) ? 0 : 1)
            .ThenBy(w => IsOrgCredit(w.WalletType) ? w.ContractExpiresAt ?? DateTimeOffset.MaxValue : DateTimeOffset.MaxValue)
            .ToList();

        var remaining = order.FinalAmountMinor;
        var allocations = new List<OrderWalletAllocationSuggestionDto>();

        foreach (var wallet in priorityWallets)
        {
            if (remaining <= 0)
                break;

            var take = Math.Min(wallet.AvailableBalanceMinor, remaining);
            if (take <= 0)
                continue;

            allocations.Add(new OrderWalletAllocationSuggestionDto(
                wallet.WalletId,
                wallet.WalletType,
                take,
                wallet.AvailableBalanceMinor));

            remaining -= take;
        }

        var totalSuggested = order.FinalAmountMinor - remaining;

        return new OrderPaymentOptionsDto(
            order.Id,
            order.OrderNumber,
            order.FinalAmountMinor,
            order.Currency,
            order.PaymentState.ToString(),
            options,
            allocations,
            totalSuggested,
            remaining <= 0,
            Math.Max(0, remaining));
    }

    private static OrderWalletOptionDto BuildWalletOption(
        WalletSummaryDto wallet,
        string orderCurrency,
        IReadOnlyList<string> orderCategoryCodes,
        DateTimeOffset now)
    {
        var isAllowed = true;
        string? reason = null;

        if (!string.Equals(wallet.Currency, orderCurrency, StringComparison.OrdinalIgnoreCase))
        {
            isAllowed = false;
            reason = "ارز کیف پول با ارز سفارش مطابقت ندارد.";
        }

        if (isAllowed && IsOrgCredit(wallet.WalletType))
        {
            if (wallet.ContractExpiresAt.HasValue && wallet.ContractExpiresAt <= now)
            {
                isAllowed = false;
                reason = "قرارداد کیف پول سازمانی منقضی شده است.";
            }
            else if (!IsAllowedForCategories(wallet.AllowedCategoryCode, orderCategoryCodes))
            {
                isAllowed = false;
                reason = "این کیف پول برای دسته بندی سفارش مجاز نیست.";
            }
        }

        return new OrderWalletOptionDto(
            wallet.WalletId,
            wallet.WalletType,
            wallet.AvailableBalanceMinor,
            wallet.TotalBalanceMinor,
            wallet.HeldAmountMinor,
            wallet.Currency,
            wallet.AllowedCategoryCode,
            wallet.ContractExpiresAt,
            isAllowed,
            reason);
    }

    private static bool IsAllowedForCategories(string? allowedCategoryCode, IReadOnlyList<string> orderCategoryCodes)
    {
        if (string.IsNullOrWhiteSpace(allowedCategoryCode))
            return true;
        if (orderCategoryCodes.Count == 0)
            return true;

        return orderCategoryCodes.All(code =>
            code.StartsWith(allowedCategoryCode, StringComparison.OrdinalIgnoreCase)
            || allowedCategoryCode.StartsWith(code, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsOrgCredit(string walletType) =>
        string.Equals(walletType, "OrgCredit", StringComparison.OrdinalIgnoreCase)
        || string.Equals(walletType, "ORG_CREDIT", StringComparison.OrdinalIgnoreCase)
        || string.Equals(walletType, "Organizational", StringComparison.OrdinalIgnoreCase);
}
