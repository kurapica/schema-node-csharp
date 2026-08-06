using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Event;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Scalar;
using SchemaNode.Schema;
using SchemaNode.Struct;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
// ReSharper disable InconsistentNaming

namespace SchemaNode.Function;

/// <summary>
/// The system reflect
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_SCHEMA_REFLECT_APP)]
public static class SystemAppReflect
{
    /// <summary>
    /// Gets the application entries
    /// </summary>
    public static async Task<List<EntryAccess<string>>> getappentries(SchemaContext context,
        [Meta<SchemaType>(typeof(AppType))] string? name = null, string? root = null)
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
    /// Gets the app field type
    /// </summary>
    public static async Task<string?> getappfieldtype(SchemaContext context,
        [Meta<SchemaType>(typeof(AppType))] string app,
        [Meta<SchemaType>(typeof(Identifier))] string field,
        bool elementType = false)
    {
        var appType = await context.GetAppTypeAsync(app);
        var fieldType = appType?.GetField(field);
        if (fieldType == null) return null;
        return elementType && fieldType.ValueType is Runtime.ArrayType arr ? arr.Element?.Name : fieldType.ValueType?.Name;
    }

    [Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_REFLECT}.event")]
    public static class Event
    {
        /// <summary>
        /// Get app field data change event payload type
        /// </summary>
        public static async Task<string> getappfieldpayload(SchemaContext context,
            [Meta<SchemaType>(typeof(AppType))] string? app,
            [Meta<SchemaType>(typeof(Identifier))] string? field)
        {
            return await getappfieldtype(context, app!, field!, true) ?? string.Empty;
        }

        /// <summary>
        /// Get app field data update event payload type
        /// </summary>
        public static async Task<string> getappfieldupdatepayload(SchemaContext context,
            [Meta<SchemaType>(typeof(AppType))] string? app,
            [Meta<SchemaType>(typeof(Identifier))] string? field)
        {
            var item = await getappfieldtype(context, app!, field!, true);
            if (string.IsNullOrWhiteSpace(item)) return string.Empty;
            return typeof(AppFieldUpdatePayload).GetSchemaType()! + $"<{item}>";
        }
    }
    
    [Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_REFLECT}.workflow")]
    public static class Workflow
    {
        /// <summary>
        /// Checks the given workflow is of the given kind
        /// </summary>
        public static async Task<bool> iskind(SchemaContext context,
            [Meta<SchemaType>(typeof(WorkflowType))] string workflow,
            [Meta<SchemaType>(typeof(WorkflowKind))] string kind)
        {
            var workflowType = await context.GetNodeTypeAsync<Runtime.WorkflowType>(workflow);
            return kind.Equals(workflowType?.WorkflowKind, StringComparison.OrdinalIgnoreCase);
        }
    }
}
