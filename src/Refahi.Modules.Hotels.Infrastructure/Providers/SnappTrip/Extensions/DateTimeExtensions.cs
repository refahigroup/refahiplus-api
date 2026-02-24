using System;

namespace Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Extensions;

public static class DateTimeExtensions
{
    public static string ToDateString(this DateTime dateTime) =>
        dateTime.ToString("yyyy-MM-dd");
        //string.Format("{0}-{1}-{2}",
        //    dateTime.Year.ToString().PadLeft(4, '0'),
        //    dateTime.Month.ToString().PadLeft(2, '0'),
        //    dateTime.Day.ToString().PadLeft(2, '0')
        //);

    public static string ToDateString(this DateOnly date) =>
        date.ToString("yyyy-MM-dd");
    //string.Format("{0}-{1}-{2}",
    //    date.Year.ToString().PadLeft(4, '0'),
    //    date.Month.ToString().PadLeft(2, '0'),
    //    date.Day.ToString().PadLeft(2, '0')
    //);
}
