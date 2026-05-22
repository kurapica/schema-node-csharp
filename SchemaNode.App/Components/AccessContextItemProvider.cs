using Microsoft.AspNetCore.Http;
using SchemaNode.Context;
using SchemaNode.Enum;
using TimeZoneConverter;

namespace SchemaNode.App.Components;

/// <summary>
/// Provides <see cref="Access"/> as a schema context item.
/// </summary>
public class AccessContextItemProvider(Access access) : ISchemaContextItemProvider<Access>
{
    public bool HasItem => true;
    public Access GetItem() => access;
}

/// <summary>
/// Carries the current request's locale, time-zone and date-format settings.
/// Registered as a scoped service and used as a context item.
/// </summary>
public class Access
{
    /// <summary>The target application name.</summary>
    public string? App { get; set; }

    /// <summary>The target object (e.g. business record).</summary>
    public string? Target { get; set; }

    /// <summary>The locale code, e.g. "enUS".</summary>
    public string Locale { get; set; } = "enUS";

    /// <summary>The time zone to use when formatting dates.</summary>
    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Local;

    /// <summary>The date/time serialization format.</summary>
    public DateFormatMode DateFormatMode { get; set; } = DateFormatMode.Iso8601;

    internal void SetAccess(string? app = null, string? target = null)
    {
        App    = app;
        Target = target;
        _stack.Clear();
    }

    internal bool Stack(string? app = null, string? target = null)
    {
        bool hasChange = App != app || Target != target;
        _stack.Push((App, Target));
        App    = app;
        Target = target;
        return hasChange;
    }

    internal bool Unstack()
    {
        if (_stack.Count > 0)
        {
            var (app, target) = _stack.Pop();
            bool hasChange = App != app || Target != target;
            App    = app;
            Target = target;
            return hasChange;
        }
        App    = null;
        Target = null;
        return true;
    }

    readonly Stack<(string? App, string? Target)> _stack = new();
}

/// <summary>RAII scope that unstacks the access target when disposed.</summary>
public sealed class AccessScope(SchemaContext context) : IDisposable
{
    public void Dispose()
    {
        var access = context.GetContextItem<Access>();
        access?.Unstack();
    }
}

/// <summary>
/// Extension methods for applying access/request-info to a <see cref="SchemaContext"/>.
/// </summary>
public static class AccessContextItemProviderExtensions
{
    /// <summary>The fallback time zone used when no explicit time zone is provided.</summary>
    public static TimeZoneInfo DefaultTimeZone { get; internal set; } = TimeZoneInfo.Local;

    /// <summary>Applies <paramref name="access"/> as the active access on the context.</summary>
    public static void SetAccess(this SchemaContext context, Access access)
    {
        var shared = context.GetContextItem<Access>();
        shared?.SetAccess(access.App, access.Target);
    }

    /// <summary>Applies app / target onto the context.</summary>
    public static void SetAccess(this SchemaContext context, string? app = null, string? target = null)
    {
        var access = context.GetContextItem<Access>();
        access?.SetAccess(app, target);
    }

    /// <summary>Pushes a new access scope (use with <c>using</c>).</summary>
    public static AccessScope StackAccess(this SchemaContext context, string? app = null, string? target = null)
    {
        var access = context.GetContextItem<Access>();
        access?.Stack(app, target);
        return new AccessScope(context);
    }

    /// <summary>
    /// Sets locale, time-zone and date-format mode from the incoming request headers/query and
    /// returns the resolved time-zone ID.
    /// </summary>
    public static string SetRequestInfo(this SchemaContext context, string? locale, string? timeZone, DateFormatMode? dateFormatMode)
    {
        var access = context.GetOrCreateContextItem<Access>();
        access.Locale         = locale ?? "enUS";
        access.DateFormatMode = dateFormatMode ?? DateFormatMode.Iso8601;

        if (!string.IsNullOrWhiteSpace(timeZone) && TZConvert.TryGetTimeZoneInfo(timeZone, out var tz))
            access.TimeZone = tz;
        else
            access.TimeZone = DefaultTimeZone;

        return access.TimeZone.Id;
    }

    /// <summary>Returns the active <see cref="TimeZoneInfo"/> from the context.</summary>
    public static TimeZoneInfo GetTimeZone(this SchemaContext context)
        => context.GetContextItem<Access>()?.TimeZone ?? TimeZoneInfo.Local;

    /// <summary>Returns the active locale code from the context.</summary>
    public static string? GetLocale(this SchemaContext context)
        => context.GetContextItem<Access>()?.Locale;

    /// <summary>Returns the active <see cref="DateFormatMode"/> from the context.</summary>
    public static DateFormatMode GetDateFormatMode(this SchemaContext context)
        => context.GetContextItem<Access>()?.DateFormatMode ?? DateFormatMode.Iso8601;

    /// <summary>Stores the default time zone (called from configuration).</summary>
    internal static void SetDefaultTimeZone(string zone)
        => DefaultTimeZone = TZConvert.GetTimeZoneInfo(zone);
}
