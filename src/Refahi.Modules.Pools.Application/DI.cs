using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Refahi.Modules.Orders.Application.Contracts.Payments;
using Refahi.Modules.Orders.Application.Contracts.Cancellation;
using Refahi.Modules.Pools.Application.Features.Purchases;
using Refahi.Modules.Pools.Application.Features.Purchases.Payments;
using Refahi.Modules.Pools.Application.Features.Purchases.Cancel;
using Refahi.Modules.Pools.Application.Features.Operators;
using Refahi.Modules.Pools.Application.Features.Vouchers;
namespace Refahi.Modules.Pools.Application;
public static class DI
{
    public static IServiceCollection RegisterApplication(this IServiceCollection services, IConfiguration config)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<PoolPurchaseAccess>().AddScoped<PoolQuoteResolver>().AddScoped<PoolOperatorAccess>().AddScoped<PoolVoucherAccess>();
        services.AddScoped<IOrderPaymentParticipant, PoolPaymentParticipant>().AddScoped<IOrderCancellationParticipant, PoolCancellationParticipant>();
        services.AddMediatR(typeof(DI).Assembly).AddValidatorsFromAssembly(typeof(DI).Assembly);
        return services;
    }
}
