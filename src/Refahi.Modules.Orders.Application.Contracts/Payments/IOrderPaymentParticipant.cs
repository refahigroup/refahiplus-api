namespace Refahi.Modules.Orders.Application.Contracts.Payments;
public sealed record OrderPaymentContext(Guid OrderId, Guid UserId, Guid? SourceReferenceId, long TotalMinor);
public interface IOrderPaymentParticipant
{
    string SourceModule { get; }
    Task<string?> GetUnavailableReasonAsync(OrderPaymentContext context,CancellationToken ct) => Task.FromResult<string?>(null);
    Task<IAsyncDisposable> AcquireAsync(OrderPaymentContext context, CancellationToken ct);
}
