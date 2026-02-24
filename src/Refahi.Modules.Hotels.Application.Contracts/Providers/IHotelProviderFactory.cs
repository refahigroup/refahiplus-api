namespace Refahi.Modules.Hotels.Application.Contracts.Providers;

/// <summary>
/// Factory برای دریافت پروایدر مناسب بر اساس نوع
/// </summary>
public interface IHotelProviderFactory
{
    /// <summary>
    /// دریافت پروایدر بر اساس نوع
    /// </summary>
    /// <param name="providerType">نوع پروایدر</param>
    /// <returns>instance از IHotelProvider</returns>
    IHotelProvider GetProvider(HotelProviderType providerType);

    /// <summary>
    /// دریافت پروایدر پیش‌فرض
    /// </summary>
    /// <returns>پروایدر پیش‌فرض (معمولاً SnappTrip)</returns>
    IHotelProvider GetDefaultProvider();
}
