using System;
using System.Collections.Generic;
using MediatR;
using Refahi.Modules.Wallets.Application.Contracts;

namespace Refahi.Modules.Wallets.Application.Contracts.Features.CreatePaymentIntent;

/// <summary>
/// Command: Create Payment Intent (Reserve).
/// Multi-wallet allocation support.
/// </summary>
public sealed record CreatePaymentIntentCommand(
    Guid OrderId,
    long AmountMinor,
    string Currency,
    List<AllocationRequest> Allocations,
    string IdempotencyKey,
    string? MetadataJson = null)
    : IRequest<CommandResponse<CreatePaymentIntentResponse>>;
