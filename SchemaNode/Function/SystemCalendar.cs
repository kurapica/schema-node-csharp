using SchemaNode.Attribute;
using SchemaNode.Components;
using SchemaNode.Context;
using System.Globalization;
using static SchemaNode.Utility.Constant;
// ReSharper disable InconsistentNaming

namespace SchemaNode.Function;

/// <summary>
/// system.calendar api
/// </summary>
[Schema(NS_SYSTEM_CALENDAR)]
public static class SystemCalendar
{
    #region Now

    [Schema]
    [NoCache]
    public static DateTimeOffset now() => DateTimeOffset.UtcNow;

    [Schema]
    [NoCache]
    public static DateTimeOffset today(SchemaContext context)
    {
        var tz = context.GetTimeZone();
        var now = ToLocal(DateTimeOffset.UtcNow, tz);
        return LocalToUtc(now.Year, now.Month, now.Day, 0, 0, 0, tz);
    }

    [Schema]
    [NoCache]
    public static DateTimeOffset tomorrow(SchemaContext context) => adddays(context, today(context), 1);

    #endregion

    #region Locale Info

    [Schema]
    public static long getsecond(SchemaContext context, DateTimeOffset dt) => ToLocal(dt, context.GetTimeZone()).Second;

    [Schema]
    public static long getminute(SchemaContext context, DateTimeOffset dt) => ToLocal(dt, context.GetTimeZone()).Minute;

    [Schema]
    public static long getday(SchemaContext context, DateTimeOffset dt) => ToLocal(dt, context.GetTimeZone()).Day;

    [Schema]
    public static long getmonth(SchemaContext context, DateTimeOffset dt) => ToLocal(dt, context.GetTimeZone()).Month;

    [Schema]
    public static long getyear(SchemaContext context, DateTimeOffset dt) => ToLocal(dt, context.GetTimeZone()).Year;

    [Schema]
    public static long getweekday(SchemaContext context, DateTimeOffset dt) => (int)ToLocal(dt, context.GetTimeZone()).DayOfWeek;

    [Schema]
    public static long getweekofyear(SchemaContext context, DateTimeOffset dt) => CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
        ToLocal(dt, context.GetTimeZone()).DateTime,
        CalendarWeekRule.FirstFourDayWeek,
        DayOfWeek.Monday
    );

    [Schema]
    public static long getquarter(SchemaContext context, DateTimeOffset dt) => (ToLocal(dt, context.GetTimeZone()).Month - 1) / 3 + 1;

    /// <summary>
    /// Gets the first time of the year
    /// </summary>
    [Schema]
    public static DateTimeOffset getfirstofyear(SchemaContext context, DateTimeOffset date)
    {
        var tz = context.GetTimeZone();
        var local = ToLocal(date, tz);
        return LocalToUtc(local.Year, 1, 1, 0, 0, 0, tz);
    }

    /// <summary>
    /// Gets the first time of the month
    /// </summary>
    [Schema]
    public static DateTimeOffset getfirstofmonth(SchemaContext context, DateTimeOffset date)
    {
        var tz = context.GetTimeZone();
        var local = ToLocal(date, tz);
        return LocalToUtc(local.Year, local.Month, 1, 0, 0, 0, tz);
    }

    /// <summary>
    /// Gets the first time of the day
    /// </summary>
    [Schema]
    public static DateTimeOffset getfirstofday(SchemaContext context, DateTimeOffset date)
    {
        var tz = context.GetTimeZone();
        var local = ToLocal(date, tz);
        return LocalToUtc(local.Year, local.Month, local.Day, 0, 0, 0, tz);
    }

    [Schema]
    public static DateTimeOffset getfirstofquarter(SchemaContext context, DateTimeOffset date)
    {
        var tz = context.GetTimeZone();
        var local = ToLocal(date, tz);
        var month = (local.Month - 1) / 3 * 3 + 1;
        return LocalToUtc(local.Year, month, 1, 0, 0, 0, tz);
    }

    [Schema]
    public static DateTimeOffset getfirstofweek(SchemaContext context, DateTimeOffset date)
    {
        var tz = context.GetTimeZone();
        var local = ToLocal(date, tz);

        int diff = (7 + (local.DayOfWeek - DayOfWeek.Monday)) % 7;
        var monday = local.AddDays(-diff);

        return LocalToUtc(monday.Year, monday.Month, monday.Day, 0, 0, 0, tz);
    }

    /// <summary>
    /// Gets the last time of the year
    /// </summary>
    [Schema]
    public static DateTimeOffset getlastofyear(SchemaContext context, DateTimeOffset date)
    {
        var tz = context.GetTimeZone();
        var local = ToLocal(date, tz);
        return LocalToUtc(local.Year + 1, 1, 1, 0, 0, 0, tz).AddSeconds(-1);
    }

    /// <summary>
    /// Gets the last time of the month
    /// </summary>
    [Schema]
    public static DateTimeOffset getlastofmonth(SchemaContext context, DateTimeOffset date)
    {
        var tz = context.GetTimeZone();
        var local = ToLocal(date, tz).AddMonths(1);
        return LocalToUtc(local.Year, local.Month, 1, 0, 0, 0, tz).AddSeconds(-1);
    }

    /// <summary>
    /// Gets the last time of the day
    /// </summary>
    [Schema]
    public static DateTimeOffset getlastofday(SchemaContext context, DateTimeOffset date)
    {
        var tz = context.GetTimeZone();
        var next = ToLocal(date, tz).AddDays(1);
        return LocalToUtc(next.Year, next.Month, next.Day, 0, 0, 0, tz).AddSeconds(-1);
    }

    [Schema]
    public static DateTimeOffset getlastofquarter(SchemaContext context, DateTimeOffset date)
    {
        var tz = context.GetTimeZone();
        var local = ToLocal(date, tz);
        var month = (local.Month - 1) / 3 * 3 + 1;
        var next = LocalToUtc(local.Year, month, 1, 0, 0, 0, tz).AddMonths(3);
        return LocalToUtc(next.Year, next.Month, 1, 0, 0, 0, tz).AddSeconds(-1);
    }

    [Schema]
    public static DateTimeOffset getlastofweek(SchemaContext context, DateTimeOffset date)
    {
        return adddays(context, getfirstofweek(context, date), 6);
    }

    #endregion

    #region Span

    /// <summary>
    /// system.calendar.getyears
    /// </summary>
    [Schema]
    public static long getyears(SchemaContext context, DateTimeOffset start, DateTimeOffset stop)
    {
        var tz = context.GetTimeZone();
        return ToLocal(stop, tz).Year - ToLocal(start, tz).Year + 1;
    }

    /// <summary>
    /// system.calendar.getmonths
    /// </summary>
    [Schema]
    public static long getmonths(SchemaContext context, DateTimeOffset start, DateTimeOffset stop)
    {
        var tz = context.GetTimeZone();
        var s = ToLocal(start, tz);
        var e = ToLocal(stop, tz);
        return (e.Month - s.Month + 1) + 12 * (e.Year - s.Year);
    }

    /// <summary>
    /// system.calendar.getdays
    /// </summary>
    [Schema]
    public static long getdays(SchemaContext context, DateTimeOffset start, DateTimeOffset stop)
    {
        var tz = context.GetTimeZone();
        var s = ToLocal(start, tz);
        var e = ToLocal(stop, tz);
        return Convert.ToInt64(e >= s
            ? Math.Ceiling((e - (s - s.TimeOfDay)).TotalDays)
            : -Math.Ceiling((s - (e - e.TimeOfDay)).TotalDays));
    }

    [Schema]
    public static long gethours(SchemaContext context, DateTimeOffset start, DateTimeOffset stop)
    {
        var tz = context.GetTimeZone();
        var s = ToLocal(start, tz);
        var e = ToLocal(stop, tz);
        return (long)(e - s).TotalHours;
    }

    [Schema]
    public static long getminutes(SchemaContext context, DateTimeOffset start, DateTimeOffset stop)
    {
        var tz = context.GetTimeZone();
        var s = ToLocal(start, tz);
        var e = ToLocal(stop, tz);
        return (long)(e - s).TotalMinutes;
    }

    [Schema]
    public static long getseconds(SchemaContext context, DateTimeOffset start, DateTimeOffset stop)
    {
        var tz = context.GetTimeZone();
        var s = ToLocal(start, tz);
        var e = ToLocal(stop, tz);
        return (long)(e - s).TotalSeconds;
    }

    /// <summary>
    /// Gets the days of a month
    /// </summary>
    [Schema]
    public static long getmonthdays(SchemaContext context, DateTimeOffset date)
    {
        var tz = context.GetTimeZone();
        var next = ToLocal(date, tz).AddMonths(1);
        return ToLocal(LocalToUtc(next.Year, next.Month, 1, 0, 0, 0, tz).AddSeconds(-1), tz).Day;
    }

    #endregion

    #region Modify

    /// <summary>
    /// system.calendar.addseconds
    /// </summary>
    [Schema]
    public static DateTimeOffset addseconds(SchemaContext context, DateTimeOffset dt, int seconds)
    {
        var tz = context.GetTimeZone();
        var local = ToLocal(dt, tz).AddSeconds(seconds);
        return LocalToUtc(local, tz);
    }

    /// <summary>
    /// system.calendar.addminutes
    /// </summary>
    [Schema]
    public static DateTimeOffset addminutes(SchemaContext context, DateTimeOffset dt, int min)
    {
        var tz = context.GetTimeZone();
        var local = ToLocal(dt, tz).AddMinutes(min);
        return LocalToUtc(local, tz);
    }

    /// <summary>
    /// system.calendar.addhours
    /// </summary>
    [Schema]
    public static DateTimeOffset addhours(SchemaContext context, DateTimeOffset dt, int hours)
    {
        var tz = context.GetTimeZone();
        var local = ToLocal(dt, tz).AddHours(hours);
        return LocalToUtc(local, tz);
    }

    /// <summary>
    /// system.calendar.adddays
    /// </summary>
    [Schema]
    public static DateTimeOffset adddays(SchemaContext context, DateTimeOffset dt, int days)
    {
        var tz = context.GetTimeZone();
        var local = ToLocal(dt, tz).AddDays(days);
        return LocalToUtc(local, tz);
    }

    /// <summary>
    /// system.calendar.addmonths
    /// </summary>
    [Schema]
    public static DateTimeOffset addmonths(SchemaContext context, DateTimeOffset dt, int months)
    {
        var tz = context.GetTimeZone();
        var local = ToLocal(dt, tz).AddMonths(months);
        return LocalToUtc(local, tz);
    }

    /// <summary>
    /// system.calendar.addyears
    /// </summary>
    [Schema]
    public static DateTimeOffset addyears(SchemaContext context, DateTimeOffset dt, int year)
    {
        var tz = context.GetTimeZone();
        var local = ToLocal(dt, tz).AddYears(year);
        return LocalToUtc(local, tz);
    }

    #endregion
    
    #region Compare
    
    /// <summary>
    /// system.calendar.eq
    /// </summary>
    [Schema]
    public static bool eq(SchemaContext context, DateTimeOffset left, DateTimeOffset right)
    {
        var tz = context.GetTimeZone();
        var l = ToLocal(left, tz);
        var r = ToLocal(right, tz);
        return l.Year == r.Year && l.DayOfYear == r.DayOfYear;
    }

    /// <summary>
    /// system.calendar.ge
    /// </summary>
    [Schema]
    public static bool ge(SchemaContext context, DateTimeOffset left, DateTimeOffset right)
    {
        var tz = context.GetTimeZone();
        var l = ToLocal(left, tz);
        var r = ToLocal(right, tz);
        return l.Year > r.Year || l.Year == r.Year && l.DayOfYear >= r.DayOfYear;
    }

    /// <summary>
    /// system.calendar.gt
    /// </summary>
    [Schema]
    public static bool gt(SchemaContext context, DateTimeOffset left, DateTimeOffset right)
    {
        var tz = context.GetTimeZone();
        var l = ToLocal(left, tz);
        var r = ToLocal(right, tz);
        return l.Year > r.Year || l.Year == r.Year && l.DayOfYear > r.DayOfYear;
    }

    /// <summary>
    /// system.calendar.le
    /// </summary>
    [Schema]
    public static bool le(SchemaContext context, DateTimeOffset left, DateTimeOffset right)
    {
        var tz = context.GetTimeZone();
        var l = ToLocal(left, tz);
        var r = ToLocal(right, tz);
        return l.Year < r.Year || l.Year == r.Year && l.DayOfYear <= r.DayOfYear;
    }

    /// <summary>
    /// system.calendar.lt
    /// </summary>
    [Schema]
    public static bool lt(SchemaContext context, DateTimeOffset left, DateTimeOffset right)
    {
        var tz = context.GetTimeZone();
        var l = ToLocal(left, tz);
        var r = ToLocal(right, tz);
        return l.Year < r.Year || l.Year == r.Year && l.DayOfYear < r.DayOfYear;
    }

    /// <summary>
    /// system.calendar.neq
    /// </summary>
    [Schema]
    public static bool neq(SchemaContext context, DateTimeOffset left, DateTimeOffset right)
    {
        var tz = context.GetTimeZone();
        var l = ToLocal(left, tz);
        var r = ToLocal(right, tz);
        return l.Year != r.Year || l.DayOfYear != r.DayOfYear;
    }

    [Schema]
    public static DateTimeOffset clamp(SchemaContext context, DateTimeOffset value, DateTimeOffset min, DateTimeOffset max)
    {
        if (lt(context, value, min)) return min;
        if (gt(context, value, max)) return max;
        return value;
    }

    [Schema]
    public static long overlapdays(SchemaContext context, DateTimeOffset a1, DateTimeOffset a2, DateTimeOffset b1, DateTimeOffset b2)
    {
        var start = gt(context, a1, b1) ? a1 : b1;
        var end = lt(context, a2, b2) ? a2 : b2;

        if (gt(context, start, end))
            return 0;

        return getdays(context, start, end);
    }

    /// <summary>
    /// system.calendar.between
    /// </summary>
    [Schema]
    public static bool between(SchemaContext context, DateTimeOffset date, DateTimeOffset min, DateTimeOffset max)
    {
        var tz = context.GetTimeZone();
        var d = ToLocal(date, tz);
        var mn = ToLocal(min, tz);
        var mx = ToLocal(max, tz);
        return (d.Year > mn.Year || d.Year == mn.Year && d.DayOfYear >= mn.DayOfYear)
               && (d.Year < mx.Year || d.Year == mx.Year && d.DayOfYear <= mx.DayOfYear);
    }

    [Schema]
    public static bool isweekend(SchemaContext context, DateTimeOffset dt)
    {
        var tz = context.GetTimeZone();
        var d = ToLocal(dt, tz).DayOfWeek;
        return d == DayOfWeek.Saturday || d == DayOfWeek.Sunday;
    }

    [Schema]
    public static bool isworkday(SchemaContext context, DateTimeOffset dt)
    {
        return !isweekend(context, dt);
    }

    [Schema]
    public static bool issameyear(SchemaContext context, DateTimeOffset a, DateTimeOffset b)
    {
        var tz = context.GetTimeZone();
        return ToLocal(a, tz).Year == ToLocal(b, tz).Year;
    }

    [Schema]
    public static bool issamemonth(SchemaContext context, DateTimeOffset a, DateTimeOffset b)
    {
        var tz = context.GetTimeZone();
        var l = ToLocal(a, tz);
        var r = ToLocal(b, tz);
        return l.Year == r.Year && l.Month == r.Month;
    }

    #endregion

    #region Util

    /// <summary>
    /// Convert a UTC DateTimeOffset to the target timezone
    /// </summary>
    static DateTimeOffset ToLocal(DateTimeOffset utc, TimeZoneInfo tz) => TimeZoneInfo.ConvertTime(utc, tz);

    /// <summary>
    /// Build a UTC DateTimeOffset from local date/time components in the given timezone
    /// </summary>
    static DateTimeOffset LocalToUtc(int year, int month, int day, int hour, int minute, int second, TimeZoneInfo tz)
    {
        var local = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);

        if (tz.IsInvalidTime(local))
            local = local.AddHours(1);

        if (tz.IsAmbiguousTime(local))
            return new DateTimeOffset(local, tz.GetAmbiguousTimeOffsets(local)[0]).ToUniversalTime();

        return TimeZoneInfo.ConvertTimeToUtc(local, tz);
    }

    /// <summary>
    /// Convert a local DateTimeOffset in the given timezone to UTC, handling invalid and ambiguous times
    /// </summary>
    /// <param name="locale"></param>
    /// <param name="tz"></param>
    /// <returns></returns>
    static DateTimeOffset LocalToUtc(DateTimeOffset locale, TimeZoneInfo tz)
    {
        var local = locale.DateTime;

        if (tz.IsInvalidTime(local))
            local = local.AddHours(1);

        var utc = TimeZoneInfo.ConvertTimeToUtc(local, tz);

        return new DateTimeOffset(utc);
    }


    #endregion
}