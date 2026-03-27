using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using SchemaNode.Enum;
using SchemaNode.Schema;

namespace SchemaNode.Utility;

internal static class App
{
    #region Methods

    /// <summary>
    /// Gets the type's system app and field
    /// </summary>
    internal static (string app, string field)? GetSystemAppField(this Type type)
    {
        if (TypeAppFieldMap.TryGetValue(type, out var result)) return result;
        return null;
    }

    /// <summary>
    /// Gets the system app
    /// </summary>
    internal static AppSchema? GetSystemApp(string appName)
    {
        appName = appName.ToLower();
        AppSchema? app = Root;
        string fullPath = "";
        foreach (string path in Regex.Split(appName, @"\W+").Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            fullPath = !string.IsNullOrWhiteSpace(fullPath) ? $"{fullPath}.{path}" : path;
            app = app.Apps?.FirstOrDefault(x => x.Name == fullPath);
            if (app == null) return null;
        }

        return new AppSchema
        {
            Name = app.Name,
            Display = app.Display,
            HasApps = app.Apps is { Length: > 0 },
            HasFields = app.Fields is { Length: > 0 },
            ScopePolicy = app.ScopePolicy,
            Auth = app.Auth,
            Auths = app.Auths,
            Apps = app.Apps?.Select(a => new AppSchema
            {
                Name = a.Name,
                Display = a.Display,
                HasApps = a.Apps is { Length: > 0 },
                HasFields = a.Fields is { Length: > 0 },
                ScopePolicy = a.ScopePolicy,
                Additional = a.Additional,
            }).ToArray(),
            Fields = app.Fields,
            Relations = app.Relations,
            Workflows = app.Workflows,
            Additional = app.Additional
        };
    }

    internal static void SaveSystemAppField(string appName, AppFieldSchema? field = null, string? display = null, Type? type = null, AppScopePolicy? policy = null)
    {
        appName = appName.ToLower();
        AppSchema app = Root;
        string fullPath = "";
        foreach (string path in Regex.Split(appName, @"\W+").Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            fullPath = !string.IsNullOrWhiteSpace(fullPath) ? $"{fullPath}.{path}" : path;
            AppSchema? next = app.Apps?.FirstOrDefault(x => x.Name == fullPath);
            if (next == null)
            {
                next = new AppSchema
                {
                    Name = fullPath,
                    LoadState = SchemaLoadState.System,
                    Display = fullPath == appName ? display : fullPath,
                    ScopePolicy = fullPath == appName ? policy : null
                };
                app.Apps = app.Apps != null ? app.Apps.Concat([next]).ToArray() : [next];
            }
            app = next;
        }

        if (field == null) return;
        app.Fields ??= [];
        app.Fields = app.Fields.Where(f => !f.Name.Equals(field.Name, StringComparison.OrdinalIgnoreCase))
            .Concat([field]).ToArray();

        if (type != null) TypeAppFieldMap[type] = (appName, field.Name);
    }

    #endregion

    #region Utility

    static readonly ConcurrentDictionary<Type, (string app, string field)> TypeAppFieldMap = [];

    #endregion

    #region System

    static readonly AppSchema Root = new ()
    {
        Name = "",
        LoadState = SchemaLoadState.System,
        Apps = []
    };

    #endregion
}