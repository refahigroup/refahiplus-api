using System;
using MediatR;
using Refahi.Modules.Wallets.Application.Contracts.Infrastructure;

namespace Refahi.Modules.Wallets.Application.Contracts.Commands;

public sealed record RepairOrphanPaymentIntentHoldCommand(
    Guid IntentId,
    Guid ExpectedOrderId,
    bool DryRun,
    string IdempotencyKey
) : IRequest<OrphanHoldRepairResult>;
