using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Exceptions;
using Refahi.Modules.Wallets.Application.Contracts.Features.CreatePaymentIntent;
using Refahi.Modules.Wallets.Application.Contracts.Infrastructure;
using Refahi.Modules.Wallets.Application.Contracts.Repositories;
using Refahi.Modules.Wallets.Domain.Aggregates;
using Refahi.Modules.Wallets.Domain.Enums;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Application.Services;

/// <summary>
/// Application Service: Create Payment Intent (Reserve) use case.
/// 
/// Responsibilities:
/// - Validate allocations (sum == total, same currency)
/// - Normalize currency
/// - Orchestrate call to Infrastructure
/// - Interpret outcome and build response
/// </summary>
public sealed class CreatePaymentIntentApplicationService
{
    private readonly IPaymentAtomicWriter _atomicWriter;
    private readonly IWalletReadRepository _walletReadRepo;

    public CreatePaymentIntentApplicationService(IPaymentAtomicWriter atomicWriter, IWalletReadRepository walletReadRepo)
    {
        _atomicWriter = atomicWriter;
        _walletReadRepo = walletReadRepo;
    }

    public async Task<CommandResponse<CreatePaymentIntentResponse>> CreateIntentAsync(
        CreatePaymentIntentCommand command,
        CancellationToken ct)
    {
        // 1) Normalize currency
        var currency = Wallet.NormalizeCurrency(command.Currency);

        // 2) Validate allocations (business invariants)
        if (command.Allocations == null || command.Allocations.Count == 0)
            throw new InvalidOperationException("Allocations cannot be empty.");

        var totalAllocationAmount = command.Allocations.Sum(a => a.AmountMinor);
        if (totalAllocationAmount != command.AmountMinor)
            throw new InvalidOperationException($"Sum of allocations ({totalAllocationAmount}) does not match total amount ({command.AmountMinor}).");

        foreach (var alloc in command.Allocations)
        {
            if (alloc.AmountMinor <= 0)
                throw new InvalidOperationException($"Allocation amount must be positive: {alloc.WalletId}");
        }

        // 3) OrgCredit wallet validation: check contract validity and category code restriction
        foreach (var alloc in command.Allocations)
        {
            var walletInfo = await _walletReadRepo.GetByIdAsync(alloc.WalletId, ct)
                ?? throw new WalletNotFoundException(alloc.WalletId);

            if (walletInfo.WalletType != (short)WalletType.OrgCredit)
                continue;

            // Contract expiry check
            if (walletInfo.ContractExpiresAt.HasValue && walletInfo.ContractExpiresAt < DateTimeOffset.UtcNow)
                throw new WalletOperationNotAllowedException(alloc.WalletId, "قرارداد کیف پول سازمانی منقضی شده است.");

            // Category restriction check
            if (walletInfo.AllowedCategoryCode is not null && command.OrderItemCategoryCode?.Count > 0)
            {
                var allowed = walletInfo.AllowedCategoryCode;
                bool allMatch = command.OrderItemCategoryCode.All(code =>
                    code.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)
                    || allowed.StartsWith(code, StringComparison.OrdinalIgnoreCase));

                if (!allMatch)
                    throw new WalletOperationNotAllowedException(alloc.WalletId, "این کیف پول سازمانی برای دسته‌بندی کالاهای سفارش مجاز نیست.");
            }
        }

        // 4) Convert to Infrastructure input
        var allocInput = command.Allocations
            .Select(a => new AllocationInput(a.WalletId, a.AmountMinor))
            .ToList();

        // 5) Delegate atomic execution to Infrastructure
        var atomicResult = await _atomicWriter.ExecuteCreateIntentAsync(
            orderId: command.OrderId,
            idempotencyKey: command.IdempotencyKey,
            totalAmountMinor: command.AmountMinor,
            currency: currency,
            allocations: allocInput,
            metadataJson: command.MetadataJson,
            ct: ct);

        // 6) Interpret outcome and build response
        return atomicResult.Outcome switch
        {
            CreateIntentOutcome.Created or CreateIntentOutcome.CreatedCached => BuildCompletedResponse(atomicResult),
            CreateIntentOutcome.InProgress => BuildInProgressResponse(),
            _ => throw new InvalidOperationException($"Unknown outcome: {atomicResult.Outcome}")
        };
    }

    private static CommandResponse<CreatePaymentIntentResponse> BuildCompletedResponse(
        CreateIntentAtomicResult atomicResult)
    {
        var allocations = atomicResult.Allocations
            .Select(a => new AllocationResponse(a.WalletId, a.AmountMinor))
            .ToList();

        var response = new CreatePaymentIntentResponse(
            IntentId: atomicResult.IntentId,
            OrderId: atomicResult.OrderId,
            AmountMinor: atomicResult.AmountMinor,
            Currency: atomicResult.Currency,
            Status: "Reserved",
            Allocations: allocations,
            CreatedAt: atomicResult.CreatedAt);

        return new CommandResponse<CreatePaymentIntentResponse>(CommandStatus.Completed, response);
    }

    private static CommandResponse<CreatePaymentIntentResponse> BuildInProgressResponse()
    {
        return new CommandResponse<CreatePaymentIntentResponse>(CommandStatus.InProgress, null);
    }
}
