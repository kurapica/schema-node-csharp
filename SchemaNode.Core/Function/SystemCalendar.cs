using SchemaNode.Attribute;
using SchemaNode.Context;
using System.Globalization;
using static SchemaNode.Utility.Constant;
using SchemaNode.Property.Core;
using SchemaNode.Property.Function;
// ReSharper disable InconsistentNaming

namespace SchemaNode.Function;

/// <summary>
/// system.calendar api
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_CALENDAR)]
public static class SystemCalendar
{
    #region Now
    [Meta<NoCache>]
    public static DateTimeOffset now() => DateTimeOffset.UtcNow;
    [Meta<NoCache>]
    public static DateTimeOffset today(SchemaContext context)
    {
        var tz = context.GetTimeZone();
        var now = ToLocal(DateTimeOffset.UtcNow, tz);
        return LocalToUtc(now.Year, now.Month, now.Day, 0, 0, 0, tz);
    }
    [Meta<NoCache>]
    public static DateTimeOffset tomorrow(SchemaContext context) => adddays(context, today(context), 1);

    #endregion

    #region Locale Info
    public static long getsecond(SchemaContext context, DateTimeOffset dt) => ToLocal(dt, context.GetTimeZone()).Second;
    public static long getminute(SchemaContext context, DateTimeOffset dt) => ToLocal(dt, context.GetTimeZone()).Minute;
    public static long getday(SchemaContext context, DateTimeOffset dt) => ToLocal(dt, context.GetTimeZone()).Day;
    public static long getmonth(SchemaContext context, DateTimeOffset dt) => ToLocal(dt, context.GetTimeZone()).Month;
    public static long getyear(SchemaContext context, DateTimeOffset dt) => ToLocal(dt, context.GetTimeZone()).Year;
    public static long getweekday(SchemaContext context, DateTimeOffset dt) => (int)ToLocal(dt, context.GetTimeZone()).DayOfWeek;
    public static long getweekofyear(SchemaContext context, DateTimeOffset dt) => CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
        ToLocal(dt, context.GetTimeZone()).DateTime,
        CalendarWeekRule.FirstFourDayWeek,
        DayOfWeek.Monday
    );
    public static long getquarter(SchemaContext context, DateTimeOffset dt) => (ToLocal(dt, context.GetTimeZone()).Month - 1) / 3 + 1;
    public static DateTimeOffset getfirstofyear(SchemaContext context, DateTimeOffset date)
    {
        var tz = context.GetTimeZone();
        var local = ToLocal(date, tz);
        return LocalToUtc(local.Year, 1, 1, 0, 0, 0, tz);
    }
    public static DateTimeOffset getfirstofmonth(SchemaContext context, DateTimeOffset date)
    {
        var tz = context.GetTimeZone();
        var local = ToLocal(date, tz);
        return LocalToUtc(local.Year, local.Month, 1, 0, 0, 0, tz);
    }
    public static DateTimeOffset getfirstofday(SchemaContext context, DateTimeOffset date)
    {
        var tz = context.GetTimeZone();
        var local = ToLocal(date, tz);
        return LocalToUtc(local.Year, local.Month, local.Day, 0, 0, 0, tz);
    }
    public static DateTimeOffset getfirstofquarter(SchemaContext context, DateTimeOffset date)
    {
        var tz = context.GetTimeZone();
        var local = ToLocal(date, tz);
        var month = (local.Month - 1) / 3 * 3 + 1;
        return LocalToUtc(local.Year, month, 1, 0, 0, 0, tz);
    }
    public static DateTimeOffset getfirstofweek(SchemaContext context, DateTimeOffset date)
    {
        var tz = context.GetTimeZone();
        var local = ToLocal(date, tz);

        int diff = (7 + (local.DayOfWeek - DayOfWeek.Monday)) % 7;
        var monday = local.AddDays(-diff);

        return LocalToUtc(monday.Year, monday.Month, monday.Day, 0, 0, 0, tz);
    }
    public static DateTimeOffset getlastofyear(SchemaContext context, DateTimeOffset date)
    {
        var tz = context.GetTimeZone();
        var local = ToLocal(date, tz);
        return LocalToUtc(local.Year + 1, 1, 1, 0, 0, 0, tz).AddSeconds(-1);
    }
    public static DateTimeOffset getlastofmonth(SchemaContext context, DateTimeOffset date)
    {
        var tz = context.GetTimeZone();
        var local = ToLocal(date, tz).AddMonths(1);
        return LocalToUtc(local.Year, local.Month, 1, 0, 0, 0, tz).AddSeconds(-1);
    }
    public static DateTimeOffset getlastofday(SchemaContext context, DateTimeOffset date)
    {
        var tz = context.GetTimeZone();
        var next = ToLocal(date, tz).AddDays(1);
        return LocalToUtc(next.Year, next.Month, next.Day, 0, 0, 0, tz).AddSeconds(-1);
    }
    public static DateTimeOffset getlastofquarter(SchemaContext context, DateTimeOffset date)
    {
        var tz = context.GetTimeZone();
        var local = ToLocal(date, tz);
        var month = (local.Month - 1) / 3 * 3 + 1;
        var next = LocalToUtc(local.Year, month, 1, 0, 0, 0, tz).AddMonths(3);
        return LocalToUtc(next.Year, next.Month, 1, 0, 0, 0, tz).AddSeconds(-1);
    }
    public static DateTimeOffset getlastofweek(SchemaContext context, DateTimeOffset date)
    {
        return adddays(context, getfirstofweek(context, date), 6);
    }

    #endregion

    #region Span
    public static long getyears(SchemaContext context, DateTimeOffset start, DateTimeOffset stop)
    {
        var tz = context.GetTimeZone();
        return ToLocal(stop, tz).Year - ToLocal(start, tz).Year + 1;
    }
    public static long getmonths(SchemaContext context, DateTimeOffset start, DateTimeOffset stop)
    {
        var tz = context.GetTimeZone();
        var s = ToLocal(start, tz);
        var e = ToLocal(stop, tz);
        return (e.Month - s.Month + 1) + 12 * (e.Year - s.Year);
    }
    public static long getdays(SchemaContext context, DateTimeOffset start, DateTimeOffset stop)
    {
        var tz = context.GetTimeZone();
        var s = ToLocal(start, tz);
        var e = ToLocal(stop, tz);
        return Convert.ToInt64(e >= s
            ? Math.Ceiling((e - (s - s.TimeOfDay)).TotalDays)
            : -Math.Ceiling((s - (e - e.TimeOfDay)).TotalDays));
    }
    public static long gethours(SchemaContext context, DateTimeOffset start, DateTimeOffset stop)
    {
        var tz = context.GetTimeZone();
        var s = ToLocal(start, tz);
        var e = ToLocal(stop, tz);
        return (long)(e - s).TotalHours;
    }
    public static long getminutes(SchemaContext context, DateTimeOffset start, DateTimeOffset stop)
    {
        var tz = context.GetTimeZone();
        var s = ToLocal(start, tz);
        var e = ToLocal(stop, tz);
        return (long)(e - s).TotalMinutes;
    }
    public static long getseconds(SchemaContext context, DateTimeOffset start, DateTimeOffset stop)
    {
        var tz = context.GetTimeZone();
        var s = ToLocal(start, tz);
        var e = ToLocal(stop, tz);
        return (long)(e - s).TotalSeconds;
    }
    public static long getmonthdays(SchemaContext context, DateTimeOffset date)
    {
        var tz = context.GetTimeZone();
        var next = ToLocal(date, tz).AddMonths(1);
        return ToLocal(LocalToUtc(next.Year, next.Month, 1, 0, 0, 0, tz).AddSeconds(-1), tz).Day;
    }

    #endregion

    #region Modify
    public static DateTimeOffset addseconds(SchemaContext context, DateTimeOffset dt, int seconds)
    {
        var tz = context.GetTimeZone();
        var local = ToLocal(dt, tz).AddSeconds(seconds);
        return LocalToUtc(local, tz);
    }
    public static DateTimeOffset addminutes(SchemaContext context, DateTimeOffset dt, int min)
    {
        var tz = context.GetTimeZone();
        var local = ToLocal(dt, tz).AddMinutes(min);
        return LocalToUtc(local, tz);
    }
    public static DateTimeOffset addhours(SchemaContext context, DateTimeOffset dt, int hours)
    {
        var tz = context.GetTimeZone();
        var local = ToLocal(dt, tz).AddHours(hours);
        return LocalToUtc(local, tz);
    }
    public static DateTimeOffset adddays(SchemaContext context, DateTimeOffset dt, int days)
    {
        var tz = context.GetTimeZone();
        var local = ToLocal(dt, tz).AddDays(days);
        return LocalToUtc(local, tz);
    }
    public static DateTimeOffset addmonths(SchemaContext context, DateTimeOffset dt, int months)
    {
        var tz = context.GetTimeZone();
        var local = ToLocal(dt, tz).AddMonths(months);
        return LocalToUtc(local, tz);
    }
    public static DateTimeOffset addyears(SchemaContext context, DateTimeOffset dt, int year)
    {
        var tz = context.GetTimeZone();
        var local = ToLocal(dt, tz).AddYears(year);
        return LocalToUtc(local, tz);
    }

    #endregion
    
    #region Compare
    public static bool eq(SchemaContext context, DateTimeOffset left, DateTimeOffset right)
    {
        var tz = context.GetTimeZone();
        var l = ToLocal(left, tz);
        var r = ToLocal(right, tz);
        return l.Year == r.Year && l.DayOfYear == r.DayOfYear;
    }
    public static bool ge(SchemaContext context, DateTimeOffset left, DateTimeOffset right)
    {
        var tz = context.GetTimeZone();
        var l = ToLocal(left, tz);
        var r = ToLocal(right, tz);
        return l.Year > r.Year || l.Year == r.Year && l.DayOfYear >= r.DayOfYear;
    }
    public static bool gt(SchemaContext context, DateTimeOffset left, DateTimeOffset right)
    {
        var tz = context.GetTimeZone();
        var l = ToLocal(left, tz);
        var r = ToLocal(right, tz);
        return l.Year > r.Year || l.Year == r.Year && l.DayOfYear > r.DayOfYear;
    }
    public static bool le(SchemaContext context, DateTimeOffset left, DateTimeOffset right)
    {
        var tz = context.GetTimeZone();
        var l = ToLocal(left, tz);
        var r = ToLocal(right, tz);
        return l.Year < r.Year || l.Year == r.Year && l.DayOfYear <= r.DayOfYear;
    }
    public static bool lt(SchemaContext context, DateTimeOffset left, DateTimeOffset right)
    {
        var tz = context.GetTimeZone();
        var l = ToLocal(left, tz);
        var r = ToLocal(right, tz);
        return l.Year < r.Year || l.Year == r.Year && l.DayOfYear < r.DayOfYear;
    }
    public static bool neq(SchemaContext context, DateTimeOffset left, DateTimeOffset right)
    {
        var tz = context.GetTimeZone();
        var l = ToLocal(left, tz);
        var r = ToLocal(right, tz);
        return l.Year != r.Year || l.DayOfYear != r.DayOfYear;
    }
    public static DateTimeOffset clamp(SchemaContext context, DateTimeOffset value, DateTimeOffset min, DateTimeOffset max)
    {
        if (lt(context, value, min)) return min;
        if (gt(context, value, max)) return max;
        return value;
    }
    public static long overlapdays(SchemaContext context, DateTimeOffset a1, DateTimeOffset a2, DateTimeOffset b1, DateTimeOffset b2)
    {
        var start = gt(context, a1, b1) ? a1 : b1;
        var end = lt(context, a2, b2) ? a2 : b2;

        if (gt(context, start, end))
            return 0;

        return getdays(context, start, end);
    }
    public static bool between(SchemaContext context, DateTimeOffset date, DateTimeOffset min, DateTimeOffset max)
    {
        var tz = context.GetTimeZone();
        var d = ToLocal(date, tz);
        var mn = ToLocal(min, tz);
        var mx = ToLocal(max, tz);
        return (d.Year > mn.Year || d.Year == mn.Year && d.DayOfYear >= mn.DayOfYear)
               && (d.Year < mx.Year || d.Year == mx.Year && d.DayOfYear <= mx.DayOfYear);
    }
    public static bool isweekend(SchemaContext context, DateTimeOffset dt)
    {
        var tz = context.GetTimeZone();
        var d = ToLocal(dt, tz).DayOfWeek;
        return d == DayOfWeek.Saturday || d == DayOfWeek.Sunday;
    }
    public static bool isworkday(SchemaContext context, DateTimeOffset dt)
    {
        return !isweekend(context, dt);
    }
    public static bool issameyear(SchemaContext context, DateTimeOffset a, DateTimeOffset b)
    {
        var tz = context.GetTimeZone();
        return ToLocal(a, tz).Year == ToLocal(b, tz).Year;
    }
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

public static class SystemCalendarExtension
{
    /// <summary>
    /// Gets the timezone
    /// </summary>
    public static TimeZoneInfo GetTimeZone(this SchemaContext context)
    {
        // Gets the shared zone
        var zone = context.GetService<TimeZoneInfo>();
        return zone ?? TimeZoneInfo.Local;
    }
}