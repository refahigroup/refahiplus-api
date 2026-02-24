using System;
using System.Collections.Generic;
using System.Text;

namespace Refahi.Shared.Extensions;

public static class DateOnlyExtensions
{
    public static string ToString(this DateOnly date, string separator = "/")
    {
        DateTime dt = new DateTime(date, new TimeOnly());
        return $"{dt.Year.ToString().PadLeft(4, '0')}{separator}{dt.Month.ToString().PadLeft(2, '0')}{separator}{dt.Day.ToString().PadLeft(2, '0')}";
    }

    public static string ToString(this DateOnly? date, string separator = "/")
    {
        if (!date.HasValue)
            return string.Empty;

        return ToString(date.Value, separator);
    }
}