#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
namespace Refahi.Modules.Wallets.Application.Contracts.Queries;

public sealed record FindOrderPaymentQuery(Guid OrderId, string IdempotencyKey) : IRequest<OrderPaymentRecovery?>;
public sealed record OrderPaymentRecovery(Guid IntentId, short Status, Guid? PaymentId, bool Refunded);
public interface IOrderPaymentRecoveryReader
{ Task<OrderPaymentRecovery?> FindAsync(Guid orderId, string key, CancellationToken ct); }
