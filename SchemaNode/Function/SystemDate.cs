using SchemaNode.Attribute;
using TimeZoneConverter;

namespace SchemaNode.Function;

/// <summary>
/// system.datetime api
/// </summary>
[SchemaType("system.datetime")]
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
    [SchemaType]
    public static DateTime Now(this DateTime dt) => DateTime.UtcNow;

    #region Locale Info

    /// <summary>
    /// system.datetime.getday
    /// </summary>
    [SchemaType]
    public static long GetLocaleDay(this DateTime dt) => dt.FromUtc().Day;

    /// <summary>
    /// system.datetime.getmonth
    /// </summary>
    [SchemaType]
    public static long GetLocaleMonth(this DateTime dt) => dt.FromUtc().Month;

    /// <summary>
    /// system.datetime.getyear
    /// </summary>
    [SchemaType]
    public static long GetLocaleYear(this DateTime dt) => dt.FromUtc().Year;

    /// <summary>
    /// Gets the first time of the year in UTC
    /// </summary>
    [SchemaType]
    public static DateTime GetFirstTimeOfYear(this DateTime date)
    {
        date = FromUtc(date);
        date = new DateTime(date.Year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return ToUtc(date);
    }

    /// <summary>
    /// Gets the first time of the mongth in UTC
    /// </summary>
    [SchemaType]
    public static DateTime GetFirstTimeOfMonth(this DateTime date)
    {
        date = FromUtc(date);
        date = new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return ToUtc(date);
    }

    /// <summary>
    /// Gets the first time of the mongth in UTC
    /// </summary>
    [SchemaType]
    public static DateTime GetFirstTimeOfDay(this DateTime date)
    {
        date = FromUtc(date);
        date = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Unspecified);
        return ToUtc(date);
    }

    /// <summary>
    /// Gets the first time of the year in UTC
    /// </summary>
    [SchemaType]
    public static DateTime GetLastTimeOfYear(this DateTime date)
    {
        date = FromUtc(date);
        date = new DateTime(date.Year + 1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified).AddSeconds(-1);
        return ToUtc(date);
    }

    /// <summary>
    /// Gets the first time of the mongth in UTC
    /// </summary>
    [SchemaType]
    public static DateTime GetLastTimeOfMonth(this DateTime date)
    {
        date = FromUtc(date).AddMonths(1);
        date = new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Unspecified).AddSeconds(-1);
        return ToUtc(date);
    }

    /// <summary>
    /// Gets the first time of the mongth in UTC
    /// </summary>
    [SchemaType]
    public static DateTime GetLastTimeOfDay(this DateTime date)
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
    [SchemaType]
    public static long GetLocaleYears(this DateTime start, DateTime stop)
    {
        start = FromUtc(start);
        stop =FromUtc(stop);
        return stop.Year - start.Year + 1;
    }

    /// <summary>
    /// system.datetime.getmonths
    /// </summary>
    [SchemaType]
    public static long GeLocaleMonths(this DateTime start, DateTime stop)
    {
        start = FromUtc(start);
        stop = FromUtc(stop);
        return (stop.Month - start.Month + 1) + 12 * (stop.Year - start.Year);
    }

    /// <summary>
    /// system.datetime.getdays
    /// </summary>
    [SchemaType]
    public static long GetLocaleDays(this DateTime start, DateTime stop)
    {
        start = FromUtc(start);
        stop = FromUtc(stop);
        return Convert.ToInt64(stop >= start ? Math.Ceiling((stop - start.Subtract(start.TimeOfDay)).TotalDays) : -Math.Ceiling((start - stop.Subtract(stop.TimeOfDay)).TotalDays));
    }

    /// <summary>
    /// Gets the days of a month
    /// </summary>
    [SchemaType]
    public static long GetMonthDays(this DateTime date)
    {
        date = FromUtc(date).AddMonths(1);
        return new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Unspecified).AddSeconds(-1).Day;
    }
    
    #endregion
    
    #region Modify
    
    /// <summary>
    /// system.datetime.adddays
    /// </summary>
    [SchemaType]
    public static DateTime AddLocaleDays(this DateTime dt, int days) => dt.FromUtc().AddDays(days).ToUtc();

    /// <summary>
    /// system.datetime.addhours
    /// </summary>
    [SchemaType]
    public static DateTime AddLocaleHours(this DateTime dt, int hours) => dt.FromUtc().AddHours(hours).ToUtc();

    /// <summary>
    /// system.datetime.addminutes
    /// </summary>
    [SchemaType]
    public static DateTime AddLocaleMinutes(this DateTime dt, int min) => dt.FromUtc().AddMinutes(min).ToUtc();

    /// <summary>
    /// system.datetime.addmonths
    /// </summary>
    [SchemaType]
    public static DateTime AddLocaleMonths(this DateTime dt, int months) => dt.FromUtc().AddMonths(months).ToUtc();

    /// <summary>
    /// system.datetime.addseconds
    /// </summary>
    [SchemaType]
    public static DateTime AddLocaleSeconds(this DateTime dt, int seconds) => dt.FromUtc().AddSeconds(seconds).ToUtc();

    /// <summary>
    /// system.datetime.addyears
    /// </summary>
    [SchemaType]
    public static DateTime AddLocaleYears(this DateTime dt, int year) => dt.FromUtc().AddYears(year).ToUtc();

    #endregion
    
    #region Compare
    
    /// <summary>
    /// system.datetime.equal
    /// </summary>
    [SchemaType]
    public static bool Equal(this DateTime left, DateTime right)
    {
        left = left.FromUtc();
        right = right.FromUtc();
        return left.Year == right.Year && left.DayOfYear == right.DayOfYear;
    }

    /// <summary>
    /// system.datetime.greateequal
    /// </summary>
    [SchemaType]
    public static bool GreatEqual(this DateTime left, DateTime right)
    {
        left = left.FromUtc();
        right = right.FromUtc();
        return left.Year > right.Year || left.Year == right.Year && left.DayOfYear >= right.DayOfYear;
    }

    /// <summary>
    /// system.datetime.greatethan
    /// </summary>
    [SchemaType]
    public static bool GreateThan(this DateTime left, DateTime right)
    {
        left = left.FromUtc();
        right = right.FromUtc();
        return left.Year > right.Year || left.Year == right.Year && left.DayOfYear > right.DayOfYear;
    }

    /// <summary>
    /// system.datetime.lessequal
    /// </summary>
    [SchemaType]
    public static bool LessEqual(this DateTime left, DateTime right)
    {
        left = left.FromUtc();
        right = right.FromUtc();
        return left.Year < right.Year || left.Year == right.Year && left.DayOfYear <= right.DayOfYear;
    }

    /// <summary>
    /// system.datetime.lessthan
    /// </summary>
    [SchemaType]
    public static bool LessThan(this DateTime left, DateTime right)
    {
        left = left.FromUtc();
        right = right.FromUtc();
        return left.Year < right.Year || left.Year == right.Year && left.DayOfYear < right.DayOfYear;
    }

    /// <summary>
    /// system.datetime.notequal
    /// </summary>
    [SchemaType]
    public static bool NotEqual(this DateTime left, DateTime right)
    {
        left = left.FromUtc();
        right = right.FromUtc();
        return left.Year != right.Year || left.DayOfYear != right.DayOfYear;
    }

    /// <summary>
    /// GreateEqual
    /// </summary>
    [SchemaType]
    public static bool Between(this DateTime date, DateTime min, DateTime max)
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