using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property.App;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Scalar;
using SchemaNode.Struct;
using static SchemaNode.Utility.AppConstant;
using AppType = SchemaNode.Schema.AppType;

// ReSharper disable InconsistentNaming

namespace SchemaNode.Function;

/// <summary>
/// The system reflect
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_SCHEMA_REFLECT_APP)]
public static class SystemReflectApp
{
    /// <summary>
    /// Gets the sub entries of the application
    /// </summary>
    public static async Task<List<EntryAccess<string>>> getaccessentries(SchemaContext context,
        [Meta<SchemaType>(typeof(AppType))] string container = "",
        [Meta<SchemaType>(typeof(Identifier))] string name = "",
        string? path = null,
        [Meta<EntryRoot>(true)]
        string? root = null)
    {
        if (!string.IsNullOrWhiteSpace(root) && !string.IsNullOrWhiteSpace(path) && !path.Equals(root, StringComparison.OrdinalIgnoreCase) && !path.StartsWith($"{root}.", StringComparison.OrdinalIgnoreCase))
            return []; // not access-able
        path ??= root;
        
        var app = !string.IsNullOrWhiteSpace(container) ? $"{container}.{name}".Trim('.') : name;
        var appType = !string.IsNullOrWhiteSpace(app) ? await context.GetAppTypeAsync(app) : null;
        if (appType == null) return [];
        
        List<Entry<string>> first = [];
        foreach (var f in appType.GetFields().Where(f => !string.IsNullOrWhiteSpace(f.Name) && !string.IsNullOrWhiteSpace(f.Type)))
        {
            Runtime.ValueType? fieldType = await context.GetNodeTypeAsync<Runtime.ValueType>(f.Type);
            if (fieldType == null) continue;
            var entry = new Entry<string> { Value = f.Name, HasChildren = fieldType.HasAccessEntries };
            entry.SetProperty<Display, LocaleString>(f.GetProperty<Display>()?.Value ?? fieldType.GetProperty<Display>()?.Value ?? f.Name);
            first.Add(entry);
        }
        
        // build the access entries
        List<EntryAccess<string>> result = [new (){ Children = first.ToArray() }];
        Entry<string>? curr = !string.IsNullOrWhiteSpace(path) ? result[0].Children!
            .FirstOrDefault(c => path.Equals(c.Value, StringComparison.OrdinalIgnoreCase) ||
                                 path.StartsWith($"{c.Value}.", StringComparison.OrdinalIgnoreCase)) : null;
        IValueTypeAccess? valueType = curr != null 
            ? await context.GetNodeTypeAsync<Runtime.ValueType>(appType.GetFields().First(f => f.Name.Equals(curr.Value, StringComparison.OrdinalIgnoreCase)).Type) 
            : null;
        
        while (valueType != null)
        {
            var accessEntry = new EntryAccess<string>();
            Entry<string>[] accesses = valueType.GetAccessEntries().ToArray();
            if (curr != null)
            {
                accessEntry.Entry = new Entry<string> { Value = curr.Value, HasChildren = accesses.Length > 0 };
                accessEntry.Entry.SetProperty<Display, LocaleString>(curr.GetProperty<Display>()?.Value ?? curr.Value);
            }
            accessEntry.Children = accesses;
            
            // check next part
            IValueTypeAccess? next = null;
            foreach (var a in accesses)
            {
                string n = a.Value;
                if (curr != null) a.Value = $"{curr.Value}.{n}";
                if (!string.IsNullOrWhiteSpace(path) && (path.Equals(a.Value, StringComparison.OrdinalIgnoreCase) || 
                                                         path.StartsWith($"{a.Value}.", StringComparison.OrdinalIgnoreCase)))
                {
                    next = valueType.GetAccessValueType(n);
                    curr = a;
                }
            }
            result.Add(accessEntry);
            valueType = next;
        } 

        // cut
        if (!string.IsNullOrWhiteSpace(root))
            result = result.SkipWhile(r => (r.Entry?.Value.Length ?? 0) < root.Length).ToList();
        return result;
    }

    /// <summary>
    /// Gets the access value type
    /// </summary>
    public static async Task<string?> getaccessvaluetype(SchemaContext context,
        [Meta<SchemaType>(typeof(AppType))] string container = "",
        [Meta<SchemaType>(typeof(Identifier))] string name = "",
        string path = "")
    {
        string[] paths = path.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        if (paths.Length == 0) return null;
        var app = !string.IsNullOrWhiteSpace(container) ? $"{container}.{name}".Trim('.') : name;
        var appType = !string.IsNullOrWhiteSpace(app) ? await context.GetAppTypeAsync(app) : null;
        var field = appType?.GetField(paths[0]);
        if (field == null || string.IsNullOrWhiteSpace(field.Type)) return null;
        Runtime.ValueType? valueType = await context.GetNodeTypeAsync<Runtime.ValueType>(field.Type);
        return paths.Length > 1 ? valueType?.GetAccessValueType(paths[1])?.Name : valueType?.Name;
    }
    
    /// <summary>
    /// Gets the application entries
    /// </summary>
    public static async Task<List<EntryAccess<string>>> getappentries(SchemaContext context,
        [Meta<SchemaType>(typeof(AppType))] string? name = null, 
        [Meta<EntryRoot>(true)] string? root = null)
    {
        if (!string.IsNullOrWhiteSpace(root) && !string.IsNullOrWhiteSpace(name) && !name.Equals(root, StringComparison.OrdinalIgnoreCase) && !name.StartsWith($"{root}.", StringComparison.OrdinalIgnoreCase))
            return []; // not access-able
        
        var app = await context.GetAppTypeAsync(string.IsNullOrWhiteSpace(name) ? (root ?? "") : name);
        if (app == null) return [];

        List<EntryAccess<string>> result = [];
        while (app != null)
        {
            var access = new EntryAccess<string>();
            if (app.Container != null)
            {
                access.Entry = new Entry<string>()
                {
                    Value = app.Name,
                    HasChildren = app.HasSubApps
                };
                access.Entry.SetProperty<Display, LocaleString>(app.GetProperty<Display>()?.Value ?? app.Name);
            }
            if (app.HasSubApps)
            {
                access.Children = app.GetSubAppSchemas().Select(s =>
                {
                    var entry = new Entry<string>
                    {
                        Value = s.FullName,
                        HasChildren = s.HasApps ?? !(s.HasFields ?? s.Fields is { Length: > 0 })
                    };
                    var display = s.GetProperty<Display>();
                    if (display != null) entry.SetProperty(display);
                    return entry;
                }).ToArray();
            }
            result.Add(access);
            app = app.Container;
            if (!string.IsNullOrWhiteSpace(root) && root.Equals(app?.Name, StringComparison.OrdinalIgnoreCase)) break;
        }
        result.Reverse();
        return result;
    }

    /// <summary>
    /// Gets the application field entries
    /// </summary>
    public static async Task<EntryAccess<string>[]> getappfields(SchemaContext context,
        [Meta<SchemaType>(typeof(AppType))] string app)
    {
        var appType = await context.GetAppTypeAsync(app);
        if (appType == null) return [];
        var access = new EntryAccess<string>
        {
            Children = appType.GetFields().Select(s =>
            {
                var entry = new Entry<string>
                {
                    Value = s.Name,
                    HasChildren = false
                };
                var display = s.GetProperty<Display>();
                if (display != null) entry.SetProperty(display);
                return entry;
            }).ToArray()
        };
        var list = new EntryAccess<string>[1];
        list[0] = access;
        return list;
    }
    
    /// <summary>
    /// Gets the application foreign field entries to the given application
    /// </summary>
    public static async Task<EntryAccess<string>[]> getappforeignfields(SchemaContext context,
        [Meta<SchemaType>(typeof(AppType))] string app,
        [Meta<SchemaType>(typeof(AppType))] string foreignApp)
    {
        var appType = await context.GetAppTypeAsync(app);
        if (appType == null) return [];
        var access = new EntryAccess<string>
        {
            Children = appType.GetFields().Where((f => 
                    f.Foreigns is { Length: > 0} && 
                    f.Foreigns.Any((fr => fr.App.Equals(foreignApp))))
                ).Select(s =>
            {
                var entry = new Entry<string>
                {
                    Value = s.Name,
                    HasChildren = false
                };
                var display = s.GetProperty<Display>();
                if (display != null) entry.SetProperty(display);
                return entry;
            }).ToArray()
        };
        var list = new EntryAccess<string>[1];
        list[0] = access;
        return list;
    }
    
    /// <summary>
    /// Checks if the application has any fields
    /// </summary>
    public static async Task<bool> hasfields(SchemaContext context,
        [Meta<SchemaType>(typeof(AppType))] string app)
    {
        var appType = await context.GetAppTypeAsync(app);
        return appType?.HasAccessEntries ?? false;
    }

    /// <summary>
    /// Gets the app field type
    /// </summary>
    public static async Task<string?> getappfieldtype(SchemaContext context,
        [Meta<SchemaType>(typeof(AppType))] string app,
        [Meta<SchemaType>(typeof(Identifier))] string field,
        bool elementType = false)
    {
        var appType = !string.IsNullOrWhiteSpace(app) ? await context.GetAppTypeAsync(app) : null;
        var fieldType = appType?.GetField(field);
        if (fieldType == null) return null;
        return elementType && fieldType.ValueType is ArrayType arr ? arr.Element?.Name : fieldType.ValueType?.Name;
    }

    /// <summary>
    /// Gets the combine fields for the given schema value type
    /// </summary>
    public static async Task<EntryAccess<string>[]> getcombinefields(SchemaContext context,
        [Meta<SchemaType>(typeof(Schema.ValueType))] string type)
    {
        var valueType = !string.IsNullOrWhiteSpace(type) ? await context.GetNodeTypeAsync<Runtime.ValueType>(type) : null;
        if (valueType == null) return [];
        var primary = (valueType as ArrayType)?.Primary;
        valueType = (valueType as ArrayType)?.Element ?? valueType;
        if (valueType is not StructType structType) return [];
        return [new EntryAccess<string>
        {
            Children =  structType.GetFields()
                .Where(f => (primary == null || !primary.Any(p => p.Equals(f.Name, StringComparison.OrdinalIgnoreCase))) &&
                            f.Type is ScalarType or EnumType)
                .Select(f =>
                {
                    var entry = new Entry<string>
                    {
                        Value = f.Name,
                        HasChildren = false
                    };
                    var display = f.GetProperty<Display>();
                    if (display != null) entry.SetProperty(display);
                    return entry;
                }).ToArray()
        }];
    }

    /// <summary>
    /// Gets the combine type for the given schema value type
    /// </summary>
    public static async Task<DataCombineType[]> getcombinetype(SchemaContext context,
        [Meta<SchemaType>(typeof(Schema.ValueType))] string type)
    {
        var valueType = !string.IsNullOrWhiteSpace(type) ? await context.GetNodeTypeAsync<Runtime.ValueType>(type) : null;
        valueType = (valueType as ArrayType)?.Element ?? valueType;
        if (valueType is EnumType or ScalarType)
        {
            if (valueType is IntType)
            {
                return [DataCombineType.Newest, DataCombineType.Oldest, DataCombineType.Sum, DataCombineType.Count];
            }
            if (valueType is DecimalType)
            {
                return [DataCombineType.Newest, DataCombineType.Oldest, DataCombineType.Sum];
            }
            return [DataCombineType.Newest, DataCombineType.Oldest];
        }
        return [];
    }

    /// <summary>
    /// Whether the application is using the given scope policy
    /// </summary>
    public static async Task<bool> isscopepolicy(SchemaContext context,
        [Meta<SchemaType>(typeof(AppType))] string app,
        AppScopeType policy)
    {
        var appType = !string.IsNullOrWhiteSpace(app) ? await context.GetAppTypeAsync(app) : null;
        var scope = appType?.GetProperty<ScopePolicy>()?.Value;
        return scope?.Type  == policy;
    }
}
