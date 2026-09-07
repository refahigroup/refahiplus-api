namespace Refahi.Modules.Orders.Application.Contracts.Cancellation;

public sealed record OrderCancellationContext(Guid OrderId, Guid UserId, string SourceModule,
    Guid? SourceReferenceId, string ReferenceType, string PaymentState, string Reason);
public sealed record OrderCancellationPreparation(bool CanContinue, string? Message = null);

public interface IOrderCancellationParticipant
{
    string SourceModule { get; }
    Task<OrderCancellationPreparation> PrepareAsync(OrderCancellationContext context, CancellationToken cancellationToken);
}
