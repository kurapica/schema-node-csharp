using SchemaNode.Enum;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;
using SchemaNode.Components;
using SchemaNode.Struct;
using TimeZoneConverter;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Context;

/// <summary>
/// The access context item provider
/// </summary>
public class AccessContextItemProvider(Access access): ISchemaContextItemProvider<Access>
{
    public bool HasItem => true;
    
    public Access GetItem() => access;
}

/// <summary>
/// The access information
/// </summary>
public class Access
{
    /// <summary>
    /// The access application
    /// </summary>
    public string? App { get; set; }

    /// <summary>
    /// The access target
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// Sets the access information, and will clear the stack and reset to the new state
    /// </summary>
    internal void SetAccess(string? app = null, string? target = null)
    {
        App = app;
        Target = target;
        _stack.Clear();
    }

    internal bool Stack(string? app = null, string? target = null)
    {
        bool hasChange = App != app || Target != target;
        _stack.Push((App, Target));
        App = app;
        Target = target;
        return hasChange;
    }
    
    internal bool Unstack()
    {
        if (_stack.Count > 0)
        {
            var (app, target) = _stack.Pop();
            bool hasChange = App != app || Target != target;
            App = app;
            Target = target;
            return hasChange;
        }
        else
        {
            App = null;
            Target = null;
            return true;
        }
    }
    
    Stack<(string? App, string? Target)> _stack = new ();
}

public sealed class AccessScope(SchemaContext context) : IDisposable
{
    public void Dispose()
    {
        var access = context.GetRequiredService<Access>();
        if (access.Unstack())
            context.GetOrCreateContextItem<PolicyEvaluatorResult>().Result.Clear();
    }
}

/// <summary>
/// The access context item provider extensions
/// </summary>
public static class AccessContextItemProviderExtensions
{
    public static void SetAccess(this SchemaContext context, Access access)
    {
        // Clear the policy evaluation cache
        context.GetOrCreateContextItem<PolicyEvaluatorResult>().Result.Clear();
        
        // Gets the shared access
        var sharedAccess = context.GetRequiredService<Access>();
        sharedAccess.SetAccess(access.App, access.Target);
    }

    /// <summary>
    /// Set the access information
    /// </summary>
    public static void SetAccess(this SchemaContext context, string? app = null, string? target = null)
    {
        // Clear the policy evaluation cache
        context.GetOrCreateContextItem<PolicyEvaluatorResult>().Result.Clear();
        
        // Gets the shared access
        var access = context.GetRequiredService<Access>();
        access.SetAccess(app, target);
    }
    
    /// <summary>
    /// Stack the access information, and will be unstacked when the context is disposed
    /// </summary>
    /// <param name="context"></param>
    /// <param name="app"></param>
    /// <param name="target"></param>
    public static AccessScope StackAccess(this SchemaContext context, string? app = null, string? target = null)
    {
        // Gets the shared access
        var access = context.GetRequiredService<Access>();
        
        // Clear the policy evaluation cache if changed
        if(access.Stack(app, target))
            context.GetOrCreateContextItem<PolicyEvaluatorResult>().Result.Clear();
            
        return new AccessScope(context);
    }

    /// <summary>
    /// Sets the request info
    /// </summary>

    public static string SetRequestInfo(this SchemaContext context, string? locale, string? timeZone, DateFormatMode? dateFormatMode)
    {
        // Gets the shared access
        var access = context.GetRequiredService<Access>();
        access.Locale = locale ?? "enUS";
        access.DateFormatMode = dateFormatMode ?? DateFormatMode.Iso8601;

        if (!string.IsNullOrWhiteSpace(timeZone) && TZConvert.TryGetTimeZoneInfo(timeZone, out var tz))
        {
            access.TimeZone = tz;
        }
        else
        {
            access.TimeZone = DefaultTimeZone;
        }
        return access.TimeZone.Id;
    }

    /// <summary>
    /// Gets the timezone
    /// </summary>
    public static TimeZoneInfo GetTimeZone(this SchemaContext context)
    {
        // Gets the shared access
        var access = context.GetRequiredService<Access>();
        return access.TimeZone ?? TimeZoneInfo.Local;
    }

    /// <summary>
    /// Gets the locale information
    /// </summary>
    public static string? GetLocale(this SchemaContext context)
    {
        // Gets the shared access
        var access = context.GetRequiredService<Access>();
        return access.Locale;
    }

    /// <summary>
    /// Gets the date time format information
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static DateFormatMode GetDateFormatMode(this SchemaContext context)
    {
        // Gets the shared access
        var access = context.GetRequiredService<Access>();
        return access.DateFormatMode;
    }

    /// <summary>
    /// Gets the locale string value
    /// </summary>
    public static string? GetLocaleString(this SchemaContext context, LocaleString? localeStr, string? locale =  null)
    {
        string? key = context.GetLocaleStringKey(localeStr, locale);
        if (key == null) return null;

        // Replace {[TOKEN]} patterns with SystemLocale values (e.g. {[LIST.PREFIX]}, {[LIST.SUFFIX]})
        if (key.Contains("{["))
        {
            string? localeKey = !string.IsNullOrWhiteSpace(locale) ? locale : context.GetLocale();
            key = Regex.Replace(key, @"\{\[([^\]]+)\]\}", m =>
                SystemLocale.GetString(m.Groups[1].Value, localeKey) ?? string.Empty);
        }

        // Replace {@type.path} patterns with the localized display name of the referenced schema type
        if (key.Contains("{@"))
        {
            key = Regex.Replace(key, @"\{\@([a-zA-Z0-9_.\-]+)\}", m =>
            {
                AnySchemaType? systemValue = context.GetSchemaTypeAsync(m.Groups[1].Value).GetAwaiter().GetResult();
                if (systemValue != null)
                    return context.GetLocaleString(systemValue.Display, locale) ?? systemValue.Name ?? string.Empty;
                return m.Value;
            });
        }

        return key;
    }
    
    static string? GetLocaleStringKey(this SchemaContext context, LocaleString? localeStr, string? locale)
    {
        if (string.IsNullOrWhiteSpace(localeStr?.Key)) return null;
        if (localeStr?.Trans == null || localeStr.Trans.Length == 0) return localeStr?.Key;
        string? localeKey = !string.IsNullOrWhiteSpace(locale) ? locale : context.GetLocale();
        if (string.IsNullOrWhiteSpace(localeKey)) return localeStr.Key;
        
        // Try contains the locale
        localeKey = localeKey.Replace("-", ""); // for simply only replace zh-CN to zhCN
        foreach (LocaleTran item in localeStr.Trans)
        {
            if (localeKey.Equals(item.Lang.Replace("-", ""), StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(item.Tran))
                return item.Tran;
        }
        return localeStr.Key;
    }

    /// <summary>
    /// Sets the time zone
    /// </summary>
    internal static void SetDefaultTimeZone(string zone) => DefaultTimeZone = TZConvert.GetTimeZoneInfo(zone);
    internal static TimeZoneInfo DefaultTimeZone = TimeZoneInfo.Local;
}