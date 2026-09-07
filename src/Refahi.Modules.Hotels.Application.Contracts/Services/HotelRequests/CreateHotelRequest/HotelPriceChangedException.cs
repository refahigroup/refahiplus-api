namespace Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.CreateHotelRequest;

public sealed class HotelPriceChangedException : Exception
{
    public HotelPriceChangedException(long currentPriceMinor)
        : base("قیمت اتاق تغییر کرده است؛ قیمت جدید را بررسی و دوباره تأیید کنید")
    {
        CurrentPriceMinor = currentPriceMinor;
    }

    public long CurrentPriceMinor { get; }
}
