using System.Text.RegularExpressions;
using SchemaNode.Enum;
using SchemaNode.Schema;

namespace SchemaNode.Utility;

internal static class App
{
    #region Methods

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
            Desc = app.Desc,
            HasApps = app.Apps is { Length: > 0 },
            HasFields = app.Fields is { Length: > 0 },
            Apps = app.Apps?.Select(a => new AppSchema
            {
                Name = a.Name,
                Display = a.Display,
                Desc = a.Desc,
                HasApps = a.Apps is { Length: > 0 },
                HasFields = a.Fields is { Length: > 0 },
            }).ToArray(),
            Fields = app.Fields,
            Relations = app.Relations
        };
    }

    internal static void SaveSystemAppField(string appName, AppFieldSchema? field = null, string? display = null)
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
                    Display = fullPath == appName ? display : fullPath
                };
                app.Apps = app.Apps != null ? app.Apps.Concat([next]).ToArray() : [next];
            }
            app = next;
        }

        if (field == null) return;
        app.Fields ??= [];
        app.Fields = app.Fields.Where(f => !f.Name.Equals(field.Name, StringComparison.OrdinalIgnoreCase))
            .Concat([field]).ToArray();
    }

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