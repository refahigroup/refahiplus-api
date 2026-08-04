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

        var postings = command.Postings ?? Array.Empty<PaymentPostingRequest>();
        foreach (var posting in postings)
        {
            if (posting.WalletId == Guid.Empty || posting.AmountMinor <= 0 ||
                posting.Direction is not 1 and not 2 || string.IsNullOrWhiteSpace(posting.Purpose))
                throw new InvalidOperationException("طرح ثبت‌های مالی معتبر نیست.");

            var wallet = await _walletReadRepo.GetByIdAsync(posting.WalletId, ct)
                ?? throw new WalletNotFoundException(posting.WalletId);
            if (!string.Equals(wallet.Currency, currency, StringComparison.Ordinal))
                throw new InvalidOperationException("واحد پول کیف ثبت مالی با پرداخت یکسان نیست.");
        }

        if (postings.Count > 0)
        {
            if (command.DestinationWalletId.HasValue)
                throw new InvalidOperationException("پرداخت چندمقصدی نمی‌تواند کیف مقصد قدیمی داشته باشد.");
            var credits = postings.Where(x => x.Direction == 1).Sum(x => x.AmountMinor);
            var debits = postings.Where(x => x.Direction == 2).Sum(x => x.AmountMinor);
            if (command.AmountMinor + debits != credits)
                throw new InvalidOperationException("طرح ثبت‌های مالی تراز نیست.");
        }

        if (command.DestinationWalletId.HasValue)
        {
            var destination = await _walletReadRepo.GetByIdAsync(command.DestinationWalletId.Value, ct)
                ?? throw new WalletNotFoundException(command.DestinationWalletId.Value);
            if (destination.WalletType != (short)WalletType.Provider)
                throw new WalletOperationNotAllowedException(command.DestinationWalletId.Value,
                    "کیف مقصد باید از نوع Provider باشد.");
            if (!string.Equals(destination.Currency, currency, StringComparison.Ordinal))
                throw new InvalidOperationException("واحد پول کیف مقصد با پرداخت یکسان نیست.");
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
            destinationWalletId: command.DestinationWalletId,
            ct: ct,
            postings: postings.Select(x => new PaymentPostingInput(
                x.WalletId, x.Direction, x.AmountMinor, x.Purpose.Trim())).ToList());

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
