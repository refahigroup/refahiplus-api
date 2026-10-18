#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Wallets.Application.Contracts.Queries;
namespace Refahi.Modules.Wallets.Application.Handlers;

public sealed class FindOrderPaymentQueryHandler(IOrderPaymentRecoveryReader reader) : IRequestHandler<FindOrderPaymentQuery, OrderPaymentRecovery?>
{ public Task<OrderPaymentRecovery?> Handle(FindOrderPaymentQuery r, CancellationToken ct) => reader.FindAsync(r.OrderId, r.IdempotencyKey, ct); }
