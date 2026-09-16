using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetWalletInfo;

namespace Refahi.Modules.Wallets.Api;

internal static class WalletEndpointAuthorization
{
    internal static async Task<bool> CanAccessWalletAsync(
        HttpContext context,
        ISender sender,
        Guid walletId,
        CancellationToken ct
    )
    {
        if (context.User.IsInRole("Admin"))
            return true;

        var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");
        if (!Guid.TryParse(subject, out var callerId))
            return false;

        var wallet = await sender.Send(new GetWalletInfoByIdQuery(walletId), ct);
        return wallet is not null && wallet.OwnerId == callerId;
    }

    internal static async Task<bool> CanAccessAllWalletsAsync(
        HttpContext context,
        ISender sender,
        IEnumerable<Guid> walletIds,
        CancellationToken ct
    )
    {
        foreach (var walletId in walletIds.Distinct())
            if (!await CanAccessWalletAsync(context, sender, walletId, ct))
                return false;
        return true;
    }
}
