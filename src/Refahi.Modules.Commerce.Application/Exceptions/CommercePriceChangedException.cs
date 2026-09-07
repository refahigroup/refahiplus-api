using Refahi.Modules.Commerce.Application.Contracts.Providers;

namespace Refahi.Modules.Commerce.Application.Exceptions;

public sealed class CommercePriceChangedException(CommerceQuoteResult current)
    : Exception("قیمت یا ظرفیت آیتم انتخاب‌شده تغییر کرده است") { public CommerceQuoteResult Current { get; } = current; }
