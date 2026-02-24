using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Hotels.Application.Contracts.Providers;

namespace Refahi.Modules.Hotels.Infrastructure.Providers;

/// <summary>
/// Factory برای ایجاد instance پروایدرهای مختلف
/// </summary>
public class HotelProviderFactory : IHotelProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly HotelProviderType _defaultProvider;

    public HotelProviderFactory(IServiceProvider serviceProvider, HotelProviderType defaultProvider = HotelProviderType.SnappTrip)
    {
        _serviceProvider = serviceProvider;
        _defaultProvider = defaultProvider;
    }

    /// <summary>
    /// دریافت پروایدر بر اساس نوع
    /// </summary>
    public IHotelProvider GetProvider(HotelProviderType providerType)
    {
        return providerType switch
        {
            HotelProviderType.SnappTrip => 
                _serviceProvider.GetRequiredService<SnappTrip.SnappTripHotelProvider>(),

            HotelProviderType.AlibabaTravels => 
                throw new NotImplementedException("AlibabaTravels provider hasn't been implemented yet"),

            _ => throw new ArgumentException($"Unknown provider type: {providerType}")
        };
    }

    /// <summary>
    /// دریافت پروایدر پیش‌فرض
    /// </summary>
    public IHotelProvider GetDefaultProvider() => GetProvider(_defaultProvider);
}
