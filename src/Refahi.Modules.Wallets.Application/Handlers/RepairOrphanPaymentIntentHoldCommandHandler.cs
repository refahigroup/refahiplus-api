using FluentValidation;
using MediatR;
using Refahi.Modules.Wallets.Application.Contracts.Commands;
using Refahi.Modules.Wallets.Application.Contracts.Infrastructure;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Application.Handlers;

public sealed class RepairOrphanPaymentIntentHoldCommandValidator : AbstractValidator<RepairOrphanPaymentIntentHoldCommand>
{
    public RepairOrphanPaymentIntentHoldCommandValidator()
    {
        RuleFor(x => x.IntentId).NotEmpty().WithMessage("شناسه رزرو الزامی است");
        RuleFor(x => x.ExpectedOrderId).NotEmpty().WithMessage("شناسه سفارش مورد انتظار الزامی است");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(200).WithMessage("کلید تکرارپذیری الزامی است");
    }
}

public sealed class RepairOrphanPaymentIntentHoldCommandHandler
    : IRequestHandler<RepairOrphanPaymentIntentHoldCommand, OrphanHoldRepairResult>
{
    private readonly IPaymentIntegrityRepairer _repairer;
    public RepairOrphanPaymentIntentHoldCommandHandler(IPaymentIntegrityRepairer repairer) => _repairer = repairer;
    public Task<OrphanHoldRepairResult> Handle(RepairOrphanPaymentIntentHoldCommand request, CancellationToken ct) =>
        _repairer.RepairOrphanHoldAsync(request.IntentId, request.ExpectedOrderId, request.DryRun, request.IdempotencyKey, ct);
}
