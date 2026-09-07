namespace Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip;

internal static class SnappTripHotelMoney
{
    internal static long ToRials(long tomanAmount, string fieldName)
    {
        if (tomanAmount <= 0)
            throw new InvalidOperationException($"قیمت {fieldName} از تامین‌کننده معتبر نیست.");

        try
        {
            return checked(tomanAmount * 10L);
        }
        catch (OverflowException ex)
        {
            throw new InvalidOperationException(
                $"قیمت {fieldName} از تامین‌کننده معتبر نیست.",
                ex
            );
        }
    }

    internal static long? ToRialsWhenPositive(long amount, string fieldName) =>
        amount > 0 ? ToRials(amount, fieldName) : null;

    internal static long ToRialsAllowZero(long tomanAmount, string fieldName)
    {
        if (tomanAmount == 0)
            return 0;

        return ToRials(tomanAmount, fieldName);
    }

    internal static bool TryToRials(long tomanAmount, out long rialAmount)
    {
        try
        {
            rialAmount = ToRials(tomanAmount, "اتاق");
            return true;
        }
        catch (InvalidOperationException)
        {
            rialAmount = 0;
            return false;
        }
    }

    internal static int ToProviderTomans(long rialAmount, string fieldName)
    {
        if (rialAmount < 0 || rialAmount % 10 != 0)
            throw new InvalidOperationException($"فیلتر {fieldName} باید مبلغ صحیح ریالی باشد.");

        return checked((int)(rialAmount / 10));
    }
}
