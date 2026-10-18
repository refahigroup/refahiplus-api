using MediatR;
using Refahi.Modules.Promotions.Domain;
using Refahi.Modules.Promotions.Application.Contracts;
namespace Refahi.Modules.Promotions.Application;

public sealed class PromotionExceptionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    { try { return await next(); } catch (PromotionRuleException ex) { throw new PromotionException(ex.Message); } }
}
