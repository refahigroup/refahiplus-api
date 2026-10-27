using Refahi.Modules.Entertainments.Application.Contracts.Providers;

namespace Refahi.Modules.Entertainments.Application.Exceptions;

public sealed class EntertainmentPriceChangedException(EntertainmentQuoteResult current)
    : Exception("قیمت یا ظرفیت آیتم انتخاب‌شده تغییر کرده است") { public EntertainmentQuoteResult Current { get; } = current; }
