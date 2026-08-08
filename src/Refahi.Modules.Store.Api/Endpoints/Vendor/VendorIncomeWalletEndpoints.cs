using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Vendor;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetMyTransactions;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetMyWallets;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Store.Api.Endpoints.Vendor;

public sealed record VendorIncomeWalletDto(
    Guid VendorId, string VendorName, Guid WalletId, string Currency, long AvailableBalanceMinor);

public sealed class VendorIncomeWalletEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/vendor/income-wallets", async (
            ClaimsPrincipal principal, IMediator mediator, CancellationToken ct) =>
        {
            if (!TryUser(principal, out var userId)) return Results.Unauthorized();
            var contexts = await mediator.Send(new GetStoreVendorContextsQuery(userId), ct);
            var result = new List<VendorIncomeWalletDto>();
            foreach (var context in contexts.Where(x => x.Permissions.Contains(StorePermissions.ViewIncomeWallet)))
            {
                var wallet = (await mediator.Send(new GetMyWalletsQuery(context.VendorId), ct))
                    .SingleOrDefault(x => x.WalletType.Equals("Provider", StringComparison.OrdinalIgnoreCase));
                if (wallet is not null)
                    result.Add(new(context.VendorId, context.VendorName, wallet.WalletId,
                        wallet.Currency, wallet.AvailableBalanceMinor));
            }
            return Results.Ok(ApiResponseHelper.Success(result));
        }).WithName("Store.Vendor.IncomeWallets").WithTags("Store.Vendor.Wallet")
          .RequireAuthorization("VendorOnly");

        routes.MapGet("/vendor/income-wallet-transactions", async (int? take,
            ClaimsPrincipal principal, IMediator mediator, CancellationToken ct) =>
        {
            if (!TryUser(principal, out var userId)) return Results.Unauthorized();
            var contexts = await mediator.Send(new GetStoreVendorContextsQuery(userId), ct);
            var result = new List<MyWalletTransactionDto>();
            foreach (var context in contexts.Where(x => x.Permissions.Contains(StorePermissions.ViewIncomeWallet)))
                result.AddRange(await mediator.Send(new GetMyWalletTransactionsQuery(
                    context.VendorId, Math.Clamp(take ?? 50, 1, 100), "Provider"), ct));
            var ordered = result
                .GroupBy(x => x.OperationId)
                .OrderByDescending(group => group.Max(x => x.CreatedAt))
                .SelectMany(group => group
                    .OrderBy(x => x.EntryType == 1 ? 0 : x.EntryType == 2 ? 1 : 2)
                    .ThenBy(x => x.OperationType == 5
                        ? -(x.PostingSequence ?? 0)
                        : x.PostingSequence ?? 0)
                    .ThenBy(x => x.LedgerEntryId))
                .Take(take ?? 50)
                .ToArray();
            return Results.Ok(ApiResponseHelper.Success(ordered));
        }).WithName("Store.Vendor.IncomeWalletTransactions").WithTags("Store.Vendor.Wallet")
          .RequireAuthorization("VendorOnly");
    }

    private static bool TryUser(ClaimsPrincipal principal, out Guid id) => Guid.TryParse(
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub"), out id);
}
