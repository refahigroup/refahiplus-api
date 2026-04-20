using MediatR;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Exceptions;
using Refahi.Modules.Wallets.Application.Contracts.Features.TopUp;
using Refahi.Modules.Wallets.Application.Contracts.Features.TopUpOrgCredit;
using Refahi.Modules.Wallets.Application.Contracts.Repositories;
using Refahi.Modules.Wallets.Domain.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Application.Features.TopUpOrgCredit;

/// <summary>
/// Handler for TopUpOrgCreditCommand.
/// Validates that the target wallet is of type OrgCredit and its contract is not expired,
/// then delegates to the standard TopUpWalletCommand pipeline.
/// </summary>
public sealed class TopUpOrgCreditCommandHandler : IRequestHandler<TopUpOrgCreditCommand, CommandResponse<TopUpWalletResponse>>
{
    private readonly IWalletReadRepository _walletReadRepo;
    private readonly ISender _sender;

    public TopUpOrgCreditCommandHandler(IWalletReadRepository walletReadRepo, ISender sender)
    {
        _walletReadRepo = walletReadRepo;
        _sender = sender;
    }

    public async Task<CommandResponse<TopUpWalletResponse>> Handle(
        TopUpOrgCreditCommand request, CancellationToken cancellationToken)
    {
        var walletInfo = await _walletReadRepo.GetByIdAsync(request.WalletId, cancellationToken)
            ?? throw new WalletNotFoundException(request.WalletId);

        if (walletInfo.WalletType != (short)WalletType.OrgCredit)
            throw new WalletOperationNotAllowedException(request.WalletId, "این عملیات فقط برای کیف پول سازمانی (OrgCredit) مجاز است.");

        if (walletInfo.ContractExpiresAt.HasValue && walletInfo.ContractExpiresAt < DateTimeOffset.UtcNow)
            throw new WalletOperationNotAllowedException(request.WalletId, "قرارداد کیف پول سازمانی منقضی شده است.");

        return await _sender.Send(new TopUpWalletCommand(
            WalletId: request.WalletId,
            AmountMinor: request.AmountMinor,
            Currency: request.Currency,
            IdempotencyKey: request.IdempotencyKey,
            MetadataJson: request.MetadataJson,
            ExternalReference: request.ExternalReference),
            cancellationToken);
    }
}
