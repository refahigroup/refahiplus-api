using Refahi.Modules.Wallets.Application.Contracts.Responses;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Application.Contracts.Repositories;

/// <summary>
/// Read-only repository for payment-related queries (CQRS Read Side).
/// NO writes, NO mutations, NO business logic.
/// </summary>
public interface IPaymentReadRepository
{
    /// <summary>
    /// Retrieves payment intent details from projection.
    /// </summary>
    /// <returns>Payment intent data or null if not found</returns>
    Task<GetPaymentIntentResponse?> GetPaymentIntentAsync(Guid intentId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves payment details from projection.
    /// </summary>
    /// <returns>Payment data or null if not found</returns>
    Task<GetPaymentResponse?> GetPaymentAsync(Guid paymentId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves refund details from projection.
    /// </summary>
    /// <returns>Refund data or null if not found</returns>
    Task<GetRefundResponse?> GetRefundAsync(Guid paymentId, Guid refundId, CancellationToken ct = default);
}
