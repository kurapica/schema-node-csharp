using SchemaNode.Attribute;
using TimeZoneConverter;
using static SchemaNode.Utility.Constant;
// ReSharper disable InconsistentNaming

namespace SchemaNode.Function;

/// <summary>
/// system.datetime api
/// </summary>
[Schema(NS_SYSTEM_DATETIME)]
public static class SystemDate
{
    /// <summary>
    /// Convert to UTC
    /// </summary>
    public static DateTime ToUtc(this DateTime date) => TimeZoneInfo.ConvertTimeToUtc(date, _timeZone);

    /// <summary>
    /// Convert from UTC
    /// </summary>
    public static DateTime FromUtc(this DateTime date) => TimeZoneInfo.ConvertTimeFromUtc(date, _timeZone);
    
    /// <summary>
    /// system.datetime.now
    /// </summary>
    [Schema]
    public static DateTime now() => DateTime.UtcNow;

    #region Locale Info

    /// <summary>
    /// system.datetime.getday
    /// </summary>
    public static long getday(DateTime dt) => dt.FromUtc().Day;

    /// <summary>
    /// system.datetime.getmonth
    /// </summary>
    [Schema]
    public static long getmonth(DateTime dt) => dt.FromUtc().Month;

    /// <summary>
    /// system.datetime.getyear
    /// </summary>
    [Schema]
    public static long getyear(DateTime dt) => dt.FromUtc().Year;

    /// <summary>
    /// Gets the first time of the year
    /// </summary>
    [Schema]
    public static DateTime getfirsttimeofyear(DateTime date)
    {
        date = FromUtc(date);
        date = new DateTime(date.Year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return ToUtc(date);
    }

    /// <summary>
    /// Gets the first time of the month
    /// </summary>
    [Schema]
    public static DateTime getfirsttimeofmonth(DateTime date)
    {
        date = FromUtc(date);
        date = new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return ToUtc(date);
    }

    /// <summary>
    /// Gets the first time of the day
    /// </summary>
    [Schema]
    public static DateTime getfirsttimeofday(DateTime date)
    {
        date = FromUtc(date);
        date = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Unspecified);
        return ToUtc(date);
    }

    /// <summary>
    /// Gets the last time of the year
    /// </summary>
    [Schema]
    public static DateTime getlasttimeofyear(DateTime date)
    {
        date = FromUtc(date);
        date = new DateTime(date.Year + 1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified).AddSeconds(-1);
        return ToUtc(date);
    }

    /// <summary>
    /// Gets the last time of the month
    /// </summary>
    [Schema]
    public static DateTime getlasttimeofmonth(DateTime date)
    {
        date = FromUtc(date).AddMonths(1);
        date = new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Unspecified).AddSeconds(-1);
        return ToUtc(date);
    }

    /// <summary>
    /// Gets the last time of the day
    /// </summary>
    [Schema]
    public static DateTime getlasttimeofday(DateTime date)
    {
        date = FromUtc(date).AddDays(1);
        date = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Unspecified).AddSeconds(-1);
        return ToUtc(date);
    }

    #endregion

    #region Span
    
    /// <summary>
    /// system.datetime.getyears
    /// </summary>
    [Schema]
    public static long getyears(DateTime start, DateTime stop)
    {
        start = FromUtc(start);
        stop = FromUtc(stop);
        return stop.Year - start.Year + 1;
    }

    /// <summary>
    /// system.datetime.getmonths
    /// </summary>
    [Schema]
    public static long getmonths(DateTime start, DateTime stop)
    {
        start = FromUtc(start);
        stop = FromUtc(stop);
        return (stop.Month - start.Month + 1) + 12 * (stop.Year - start.Year);
    }

    /// <summary>
    /// system.datetime.getdays
    /// </summary>
    [Schema]
    public static long getdays(DateTime start, DateTime stop)
    {
        start = FromUtc(start);
        stop = FromUtc(stop);
        return Convert.ToInt64(stop >= start ? Math.Ceiling((stop - start.Subtract(start.TimeOfDay)).TotalDays) : -Math.Ceiling((start - stop.Subtract(stop.TimeOfDay)).TotalDays));
    }

    /// <summary>
    /// Gets the days of a month
    /// </summary>
    [Schema]
    public static long getmonthdays(DateTime date)
    {
        date = FromUtc(date).AddMonths(1);
        return new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Unspecified).AddSeconds(-1).Day;
    }
    
    #endregion
    
    #region Modify
    
    /// <summary>
    /// system.datetime.adddays
    /// </summary>
    [Schema]
    public static DateTime adddays(DateTime dt, int days) => dt.FromUtc().AddDays(days).ToUtc();

    /// <summary>
    /// system.datetime.addhours
    /// </summary>
    [Schema]
    public static DateTime addhours(DateTime dt, int hours) => dt.FromUtc().AddHours(hours).ToUtc();

    /// <summary>
    /// system.datetime.addminutes
    /// </summary>
    [Schema]
    public static DateTime addminutes(DateTime dt, int min) => dt.FromUtc().AddMinutes(min).ToUtc();

    /// <summary>
    /// system.datetime.addmonths
    /// </summary>
    [Schema]
    public static DateTime addmonths(DateTime dt, int months) => dt.FromUtc().AddMonths(months).ToUtc();

    /// <summary>
    /// system.datetime.addseconds
    /// </summary>
    [Schema]
    public static DateTime addseconds(DateTime dt, int seconds) => dt.FromUtc().AddSeconds(seconds).ToUtc();

    /// <summary>
    /// system.datetime.addyears
    /// </summary>
    [Schema]
    public static DateTime addyears(DateTime dt, int year) => dt.FromUtc().AddYears(year).ToUtc();

    #endregion
    
    #region Compare
    
    /// <summary>
    /// system.datetime.equal
    /// </summary>
    [Schema]
    public static bool equal(DateTime left, DateTime right)
    {
        left = left.FromUtc();
        right = right.FromUtc();
        return left.Year == right.Year && left.DayOfYear == right.DayOfYear;
    }

    /// <summary>
    /// system.datetime.greateequal
    /// </summary>
    [Schema]
    public static bool greateequal(DateTime left, DateTime right)
    {
        left = left.FromUtc();
        right = right.FromUtc();
        return left.Year > right.Year || left.Year == right.Year && left.DayOfYear >= right.DayOfYear;
    }

    /// <summary>
    /// system.datetime.greatethan
    /// </summary>
    [Schema]
    public static bool greatethan(DateTime left, DateTime right)
    {
        left = left.FromUtc();
        right = right.FromUtc();
        return left.Year > right.Year || left.Year == right.Year && left.DayOfYear > right.DayOfYear;
    }

    /// <summary>
    /// system.datetime.lessequal
    /// </summary>
    [Schema]
    public static bool lessequal(DateTime left, DateTime right)
    {
        left = left.FromUtc();
        right = right.FromUtc();
        return left.Year < right.Year || left.Year == right.Year && left.DayOfYear <= right.DayOfYear;
    }

    /// <summary>
    /// system.datetime.lessthan
    /// </summary>
    [Schema]
    public static bool lessthan(DateTime left, DateTime right)
    {
        left = left.FromUtc();
        right = right.FromUtc();
        return left.Year < right.Year || left.Year == right.Year && left.DayOfYear < right.DayOfYear;
    }

    /// <summary>
    /// system.datetime.notequal
    /// </summary>
    [Schema]
    public static bool notequal(DateTime left, DateTime right)
    {
        left = left.FromUtc();
        right = right.FromUtc();
        return left.Year != right.Year || left.DayOfYear != right.DayOfYear;
    }

    /// <summary>
    /// Between
    /// </summary>
    [Schema]
    public static bool between(DateTime date, DateTime min, DateTime max)
    {
        date = FromUtc(date);
        min = FromUtc(min);
        max = FromUtc(max);
        return (date.Year > min.Year || date.Year == min.Year && date.DayOfYear >= min.DayOfYear)
               && (date.Year < max.Year || date.Year == max.Year && date.DayOfYear <= max.DayOfYear);
    }
    
    #endregion
    
    #region Utility

    /// <summary>
    /// Sets the time zone
    /// </summary>
    public static void SetTimeZone(string zone) => _timeZone = TZConvert.GetTimeZoneInfo(zone);
    static TimeZoneInfo _timeZone = TZConvert.GetTimeZoneInfo(DEFAULT_TIMEZONE);
    
    // ReSharper disable once InconsistentNaming
    const string DEFAULT_TIMEZONE = "China Standard Time";
    
    #endregion
}