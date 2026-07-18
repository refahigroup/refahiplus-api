using MediatR;
using Refahi.Modules.Wallets.Application.Contracts.Infrastructure;
using System;

namespace Refahi.Modules.Wallets.Application.Contracts.Commands;

public sealed record RepairOrphanPaymentIntentHoldCommand(
    Guid IntentId, Guid ExpectedOrderId, bool DryRun, string IdempotencyKey)
    : IRequest<OrphanHoldRepairResult>;
