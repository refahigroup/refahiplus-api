using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using Refahi.Modules.Wallets.Application.Contracts.Repositories;
using Refahi.Modules.Wallets.Application.Contracts.Responses;

namespace Refahi.Modules.Wallets.Infrastructure.Persistence.Repositories;

/// <summary>
/// Read-only repository for payment queries using Dapper (CQRS Read Side).
/// NO writes, NO transactions, NO business logic.
/// </summary>
public sealed class PaymentReadRepository : IPaymentReadRepository
{
    private readonly string _connectionString;

    public PaymentReadRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<GetPaymentIntentResponse?> GetPaymentIntentAsync(
        Guid intentId,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Query payment intent main data
        var intentData = await conn.QuerySingleOrDefaultAsync<PaymentIntentRow>(
            new CommandDefinition(
                commandText: @"
                    SELECT 
                        intent_id AS IntentId,
                        order_id AS OrderId,
                        status AS Status,
                        amount_minor AS AmountMinor,
                        currency AS Currency,
                        created_at AS CreatedAt,
                        captured_at AS CapturedAt,
                        released_at AS ReleasedAt
                    FROM wallets.payment_intents
                    WHERE intent_id = @IntentId",
                parameters: new { IntentId = intentId },
                cancellationToken: ct));

        if (intentData == null)
            return null;

        // Query allocations (NO ledger_entry_id for intents - only reserve, not payment)
        var allocations = await conn.QueryAsync<AllocationRow>(
            new CommandDefinition(
                commandText: @"
                    SELECT 
                        wallet_id AS WalletId,
                        amount_minor AS AmountMinor,
                        NULL AS LedgerEntryId
                    FROM wallets.payment_intent_allocations
                    WHERE intent_id = @IntentId
                    ORDER BY sequence",
                parameters: new { IntentId = intentId },
                cancellationToken: ct));

        return new GetPaymentIntentResponse(
            IntentId: intentData.IntentId,
            OrderId: intentData.OrderId,
            Status: MapIntentStatus(intentData.Status),
            AmountMinor: intentData.AmountMinor,
            Currency: intentData.Currency,
            Allocations: allocations.Select(a => new AllocationDto(
                WalletId: a.WalletId,
                AmountMinor: a.AmountMinor,
                LedgerEntryId: a.LedgerEntryId)).ToList(),
            CreatedAt: intentData.CreatedAt,
            CompletedAt: intentData.CapturedAt,
            ReleasedAt: intentData.ReleasedAt
        );
    }

    public async Task<GetPaymentResponse?> GetPaymentAsync(
        Guid paymentId,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Query payment main data
        var paymentData = await conn.QuerySingleOrDefaultAsync<PaymentRow>(
            new CommandDefinition(
                commandText: @"
                    SELECT 
                        payment_id AS PaymentId,
                        intent_id AS IntentId,
                        order_id AS OrderId,
                        status AS Status,
                        amount_minor AS AmountMinor,
                        currency AS Currency,
                        completed_at AS CompletedAt
                    FROM wallets.payments
                    WHERE payment_id = @PaymentId",
                parameters: new { PaymentId = paymentId },
                cancellationToken: ct));

        if (paymentData == null)
            return null;

        // Query allocations (includes ledger_entry_id)
        var allocations = await conn.QueryAsync<AllocationRow>(
            new CommandDefinition(
                commandText: @"
                    SELECT 
                        wallet_id AS WalletId,
                        amount_minor AS AmountMinor,
                        ledger_entry_id AS LedgerEntryId
                    FROM wallets.payment_allocations
                    WHERE payment_id = @PaymentId
                    ORDER BY sequence",
                parameters: new { PaymentId = paymentId },
                cancellationToken: ct));

        return new GetPaymentResponse(
            PaymentId: paymentData.PaymentId,
            IntentId: paymentData.IntentId ?? Guid.Empty,
            OrderId: paymentData.OrderId,
            Status: MapPaymentStatus(paymentData.Status),
            AmountMinor: paymentData.AmountMinor,
            Currency: paymentData.Currency,
            Allocations: allocations.Select(a => new AllocationDto(
                WalletId: a.WalletId,
                AmountMinor: a.AmountMinor,
                LedgerEntryId: a.LedgerEntryId)).ToList(),
            CompletedAt: paymentData.CompletedAt
        );
    }

    public async Task<GetRefundResponse?> GetRefundAsync(
        Guid paymentId,
        Guid refundId,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Query refund main data
        var refundData = await conn.QuerySingleOrDefaultAsync<RefundRow>(
            new CommandDefinition(
                commandText: @"
                    SELECT 
                        refund_id AS RefundId,
                        payment_id AS PaymentId,
                        order_id AS OrderId,
                        status AS Status,
                        amount_minor AS AmountMinor,
                        currency AS Currency,
                        reason AS Reason,
                        completed_at AS CompletedAt
                    FROM wallets.refunds
                    WHERE refund_id = @RefundId AND payment_id = @PaymentId",
                parameters: new { RefundId = refundId, PaymentId = paymentId },
                cancellationToken: ct));

        if (refundData == null)
            return null;

        // Query allocations (includes ledger_entry_id)
        var allocations = await conn.QueryAsync<AllocationRow>(
            new CommandDefinition(
                commandText: @"
                    SELECT 
                        wallet_id AS WalletId,
                        amount_minor AS AmountMinor,
                        ledger_entry_id AS LedgerEntryId
                    FROM wallets.refund_allocations
                    WHERE refund_id = @RefundId
                    ORDER BY sequence",
                parameters: new { RefundId = refundId },
                cancellationToken: ct));

        return new GetRefundResponse(
            RefundId: refundData.RefundId,
            PaymentId: refundData.PaymentId,
            OrderId: refundData.OrderId,
            Status: MapRefundStatus(refundData.Status),
            AmountMinor: refundData.AmountMinor,
            Currency: refundData.Currency,
            Allocations: allocations.Select(a => new AllocationDto(
                WalletId: a.WalletId,
                AmountMinor: a.AmountMinor,
                LedgerEntryId: a.LedgerEntryId)).ToList(),
            CompletedAt: refundData.CompletedAt,
            Reason: refundData.Reason
        );
    }

    // ===================================================================
    // PRIVATE MAPPING HELPERS (Read-only, no business logic)
    // ===================================================================
    private static string MapIntentStatus(short status) => status switch
    {
        1 => "Reserved",
        2 => "Captured",
        3 => "Released",
        _ => $"Unknown({status})"
    };

    private static string MapPaymentStatus(short status) => status switch
    {
        1 => "Completed",
        _ => $"Unknown({status})"
    };

    private static string MapRefundStatus(short status) => status switch
    {
        1 => "Completed",
        _ => $"Unknown({status})"
    };

    // ===================================================================
    // INTERNAL DTOs (Dapper mapping targets)
    // ===================================================================
    private sealed record PaymentIntentRow(
        Guid IntentId,
        Guid OrderId,
        short Status,
        long AmountMinor,
        string Currency,
        DateTimeOffset CreatedAt,
        DateTimeOffset? CapturedAt,
        DateTimeOffset? ReleasedAt);

    private sealed record PaymentRow(
        Guid PaymentId,
        Guid? IntentId,
        Guid OrderId,
        short Status,
        long AmountMinor,
        string Currency,
        DateTimeOffset CompletedAt);

    private sealed record RefundRow(
        Guid RefundId,
        Guid PaymentId,
        Guid OrderId,
        short Status,
        long AmountMinor,
        string Currency,
        string? Reason,
        DateTimeOffset CompletedAt);

    private sealed record AllocationRow(
        Guid WalletId,
        long AmountMinor,
        Guid? LedgerEntryId);
}
