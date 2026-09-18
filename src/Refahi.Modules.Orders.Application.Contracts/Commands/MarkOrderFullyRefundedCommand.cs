using MediatR;

namespace Refahi.Modules.Orders.Application.Contracts.Commands;

public sealed record MarkOrderFullyRefundedCommand(Guid OrderId, Guid WalletPaymentId,
    string SourceReference) : IRequest<MarkOrderFullyRefundedResponse>;
public sealed record MarkOrderFullyRefundedResponse(Guid OrderId, string Status);
