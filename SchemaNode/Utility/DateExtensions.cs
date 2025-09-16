using TimeZoneConverter;

namespace SchemaNode.Utility;

/// <summary>
/// Date extension
/// </summary>
public static class DateExtensions
{
    /// <summary>
    /// Sets the time zone
    /// </summary>
    public static void SetTimeZone(string zone) => _timeZone = TZConvert.GetTimeZoneInfo(zone);

    #region Get Date

    /// <summary>
    /// Locale Time Now
    /// </summary>
    /// <returns></returns>
    public static DateTime Now() => DateTime.UtcNow.FromUtc();

    /// <summary>
    /// UTC Time Now
    /// </summary>
    public static DateTime UtcNow() => DateTime.UtcNow;

    #endregion

    #region Conversion

    /// <summary>
    /// Convert to UTC
    /// </summary>
    public static DateTime ToUtc(this DateTime date) => TimeZoneInfo.ConvertTimeToUtc(date, _timeZone);

    /// <summary>
    /// Convert from UTC
    /// </summary>
    public static DateTime FromUtc(this DateTime date) => TimeZoneInfo.ConvertTimeFromUtc(date, _timeZone);

    /// <summary>
    /// Gets the year
    /// </summary>
    public static long GetYear(this DateTime date) => Convert.ToInt64(FromUtc(date).Year);

    /// <summary>
    /// Gets the month
    /// </summary>
    public static long GetMonth(this DateTime date) => Convert.ToInt64(FromUtc(date).Month);

    /// <summary>
    /// Gets the day
    /// </summary>
    public static long GetDay(this DateTime date) => Convert.ToInt64(FromUtc(date).Day);

    /// <summary>
    /// Gets the first time of the year in UTC
    /// </summary>
    public static DateTime GetFirstTimeOfYear(this DateTime date)
    {
        date = FromUtc(date);
        date = new DateTime(date.Year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return ToUtc(date);
    }

    /// <summary>
    /// Gets the first time of the mongth in UTC
    /// </summary>
    public static DateTime GetFirstTimeOfMonth(this DateTime date)
    {
        date = FromUtc(date);
        date = new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return ToUtc(date);
    }

    /// <summary>
    /// Gets the first time of the mongth in UTC
    /// </summary>
    public static DateTime GetFirstTimeOfDay(this DateTime date)
    {
        date = FromUtc(date);
        date = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Unspecified);
        return ToUtc(date);
    }

    /// <summary>
    /// Gets the first time of the year in UTC
    /// </summary>
    public static DateTime GetLastTimeOfYear(this DateTime date)
    {
        date = FromUtc(date);
        date = new DateTime(date.Year + 1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified).AddSeconds(-1);
        return ToUtc(date);
    }

    /// <summary>
    /// Gets the first time of the mongth in UTC
    /// </summary>
    public static DateTime GetLastTimeOfMonth(this DateTime date)
    {
        date = FromUtc(date).AddMonths(1);
        date = new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Unspecified).AddSeconds(-1);
        return ToUtc(date);
    }

    /// <summary>
    /// Gets the first time of the mongth in UTC
    /// </summary>
    public static DateTime GetLastTimeOfDay(this DateTime date)
    {
        date = FromUtc(date).AddDays(1);
        date = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Unspecified).AddSeconds(-1);
        return ToUtc(date);
    }

    #endregion

    #region Get TimeSpan

    /// <summary>
    /// Gets the years between two date
    /// </summary>
    public static long GetYears(this DateTime start, DateTime stop)
    {
        start = FromUtc(start);
        stop =FromUtc(stop);
        return stop.Year - start.Year + 1;
    }

    /// <summary>
    /// Gets the months between two date
    /// </summary>
    public static long GetMonths(this DateTime start, DateTime stop)
    {
        start = FromUtc(start);
        stop = FromUtc(stop);
        return (stop.Month - start.Month + 1) + 12 * (stop.Year - start.Year);
    }

    /// <summary>
    /// Gets the days between two date
    /// </summary>
    public static long GetDays(this DateTime start, DateTime stop)
    {
        start = FromUtc(start);
        stop = FromUtc(stop);
        return Convert.ToInt64(stop >= start ? Math.Ceiling((stop - start.Subtract(start.TimeOfDay)).TotalDays) : -Math.Ceiling((start - stop.Subtract(stop.TimeOfDay)).TotalDays));
    }

    /// <summary>
    /// Gets the days of a month
    /// </summary>
    public static long GetMonthDays(this DateTime date)
    {
        date = FromUtc(date).AddMonths(1);
        return new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Unspecified).AddSeconds(-1).Day;
    }

    #endregion

    #region Modify

    /// <summary>
    /// Add year
    /// </summary>
    public static DateTime AddYears(this DateTime date, long count) => date.AddYears(Convert.ToInt32(count));

    /// <summary>
    /// Add months
    /// </summary>
    public static DateTime AddMonths(this DateTime date, long count) => date.AddMonths(Convert.ToInt32(count));

    /// <summary>
    /// Add days
    /// </summary>
    public static DateTime AddDays(this DateTime date, long count) => date.AddDays(Convert.ToInt32(count));

    /// <summary>
    /// Add hours
    /// </summary>
    public static DateTime AddHours(this DateTime date, long count) => date.AddHours(Convert.ToInt32(count));

    /// <summary>
    /// Add hours
    /// </summary>
    public static DateTime AddMinutes(this DateTime date, long count) => date.AddMinutes(Convert.ToInt32(count));

    /// <summary>
    /// Add hours
    /// </summary>
    public static DateTime AddSeconds(this DateTime date, long count) => date.AddSeconds(Convert.ToInt32(count));

    #endregion

    #region Compare

    /// <summary>
    /// LessThan
    /// </summary>
    public static bool LessThan(this DateTime left, DateTime right)
    {
        left = FromUtc(left);
        right = FromUtc(right);
        return left.Year < right.Year || left.Year == right.Year && left.DayOfYear < right.DayOfYear;
    }

    /// <summary>
    /// LessEqual
    /// </summary>
    public static bool LessEqual(this DateTime left, DateTime right)
    {
        left = FromUtc(left);
        right = FromUtc(right);
        return left.Year < right.Year || left.Year == right.Year && left.DayOfYear <= right.DayOfYear;
    }

    /// <summary>
    /// Equal
    /// </summary>
    public static bool Equal(this DateTime left, DateTime right)
    {
        left = FromUtc(left);
        right = FromUtc(right);
        return left.Year == right.Year && left.DayOfYear == right.DayOfYear;
    }

    /// <summary>
    /// NotEqual
    /// </summary>
    public static bool NotEqual(this DateTime left, DateTime right)
    {
        left = FromUtc(left);
        right = FromUtc(right);
        return left.Year != right.Year || left.DayOfYear != right.DayOfYear;
    }

    /// <summary>
    /// GreateThan
    /// </summary>
    public static bool GreateThan(this DateTime left, DateTime right)
    {
        left = FromUtc(left);
        right = FromUtc(right);
        return left.Year > right.Year || left.Year == right.Year && left.DayOfYear > right.DayOfYear;
    }

    /// <summary>
    /// GreateEqual
    /// </summary>
    public static bool GreateEqual(this DateTime left, DateTime right)
    {
        left = FromUtc(left);
        right = FromUtc(right);
        return left.Year > right.Year || left.Year == right.Year && left.DayOfYear >= right.DayOfYear;
    }

    /// <summary>
    /// GreateEqual
    /// </summary>
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

    static TimeZoneInfo _timeZone = TZConvert.GetTimeZoneInfo("China Standard Time");
    
    #endregion
}