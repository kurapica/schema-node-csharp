using System.Text.RegularExpressions;
using SchemaNode.Context;
using SchemaNode.Runtime;
using SchemaNode.Schema;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Components;

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
    /// The locale information
    /// </summary>
    public string? Locale { get; set; }
    
    /// <summary>
    /// Sets the access information, and will clear the stack and reset to the new state
    /// </summary>
    /// <param name="app"></param>
    /// <param name="target"></param>
    /// <param name="locale"></param>
    internal void SetAccess(string? app = null, string? target = null, string? locale = null)
    {
        App = app;
        Target = target;
        Locale = locale;
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
    /// <summary>
    /// Set the access information
    /// </summary>
    public static void SetAccess(this SchemaContext context, string? app = null, string? target = null, string? locale = null)
    {
        // Clear the policy evaluation cache
        context.GetOrCreateContextItem<PolicyEvaluatorResult>().Result.Clear();
        
        // Gets the shared access
        var access = context.GetRequiredService<Access>();
        access.SetAccess(app, target, locale);
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
    /// Set the locale information
    /// </summary>
    public static void SetLocale(this SchemaContext context, string? locale)
    {
        // Gets the shared access
        var access = context.GetRequiredService<Access>();
        access.Locale = locale;
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
    /// Gets the locale string value
    /// </summary>
    public static string? GetLocaleString(this SchemaContext context, LocaleString? locale)
    {
        string? key = context.GetLocaleStringKey(locale);
        // Fetch the replace type like {@system.xxx}
        if (key != null && key.Contains("{@"))
        {
            Match match = System.Text.RegularExpressions.Regex.Match(key, @"\{\@([a-zA-Z0-9_.\-]+)\}");
            if (match.Success && match.Groups.Count > 1)
            {
                string systemKey = match.Groups[1].Value;
                AnySchemaType? systemValue = context.GetSchemaTypeAsync(systemKey).GetAwaiter().GetResult();
                if (systemValue != null)
                    return context.GetLocaleString(systemValue.Display) ?? systemValue.Name;
            }
        }

        return key;
    }
    
    static string? GetLocaleStringKey(this SchemaContext context, LocaleString? locale)
    {
        if (string.IsNullOrWhiteSpace(locale?.Key)) return null;
        if (locale?.Trans == null || locale.Trans.Length == 0) return locale?.Key;
        string? localeKey = context.GetLocale();
        if (string.IsNullOrWhiteSpace(localeKey)) return locale.Key;
        
        // Try match the locale
        localeKey = localeKey.Replace("-", ""); // for simply only replace zh-CN to zhCN
        foreach (LocaleTran item in locale.Trans)
        {
            if (localeKey.Equals(item.Lang.Replace("-", ""), StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(item.Tran))
                return item.Tran;
        }
        return locale.Key;
    }
}