using System.Collections.Concurrent;
using SchemaNode.Http;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Struct;
using SchemaNode.Utility;
using AppType = SchemaNode.Runtime.AppType;

namespace SchemaNode.Context;

/// <summary>
/// The application schema run time
/// </summary>
public class AppSchemaRuntime : SchemaRuntime
{
    #region System App schema

    private readonly AppSchema _rootAppSchema = new();
    private readonly ConcurrentDictionary<string, Type> _appFieldTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Type, (string App, string Field)> _typeAppFields = new();
    private readonly ConcurrentDictionary<string, Runtime.AppType> _apps = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Save system app schema
    /// </summary>
    internal void SaveSystemAppSchema(AppSchema schema)
    {
        string schemaName = schema.FullName.ToLowerInvariant();
        AppSchema root = _rootAppSchema;
        string fullPath = "";

        SpanReader reader = schemaName;
        while(reader.NextNamespace())
        {
            string container = fullPath;
            string part = reader.Current.ToString();
            fullPath = !string.IsNullOrWhiteSpace(fullPath) ? $"{fullPath}.{part}" : part;

            AppSchema? node = root.Apps?.FirstOrDefault(x => x.Name.Equals(part, StringComparison.OrdinalIgnoreCase));
            if (node == null)
            {
                if (schemaName == fullPath)
                {
                    // Target node: add it
                    root.Apps = root.Apps != null ? root.Apps.Concat([schema]).ToArray() : [schema];
                }
                else
                {
                    if (root.Fields is { Length: > 0 }) throw new Exception($"System app {root.FullName} can't be used as app container");
                    
                    // Intermediate namespace: create it
                    node = new AppSchema
                    {
                        Name = part,
                        Container = container
                    };
                    node.SetProperty<Display, LocaleString>(node.FullName);
                    
                    root.Apps = root.Apps != null ? root.Apps.Concat([node]).ToArray() : [node];
                    root = node;
                    root.Apps ??= [];
                }
            }
            else if (schemaName != fullPath)
            {
                root = node;
                root.Apps ??= [];
            }
            // override the extension properties
            else
            {
                node.CombineExtensions(schema, this);
            }
        }
    }

    /// <summary>
    /// Save system app field schema
    /// </summary>
    internal void SaveSystemAppFieldSchema(AppFieldSchema schema, Type? matchType)
    {
        AppSchema app = GetSystemAppSchema(schema.App, true)!;
        if (app.Apps is { Length: > 0 })
            throw new InvalidOperationException($"System app {app.FullName} is used as app container, can't have fields");
        app.Fields ??= [];
        var index = Array.FindIndex(app.Fields, f => f.Name.Equals(schema.Name, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            app.Fields[index] = schema;
        }
        else
            app.Fields = app.Fields is { Length: > 0 } ? app.Fields.Concat([schema]).ToArray() : [schema];

        if (matchType != null)
        {
            _appFieldTypes[$"{schema.App}.{schema.Name}"] = matchType;
            _typeAppFields[matchType] = (schema.App, schema.Name);
        }
    }
    
    /// <summary>
    /// Save the system applications
    /// </summary>
    internal void SaveSystemApp(AppType app) => _apps[app.Name] = app;
    
    /// <summary>
    /// Gets system app schema
    /// </summary>
    internal AppSchema? GetSystemAppSchema(string name, bool createIfNotExists = false)
    {
        AppSchema? node = _rootAppSchema;
        SpanReader reader = name;
        while (node != null && reader.NextNamespace())
        {
            ReadOnlySpan<char> part = reader.Current;
            AppSchema? curr = null;
            if (node.Apps != null)
            {
                foreach (var schema in node.Apps)
                {
                    if (!part.SeqEquals(schema.Name, StringComparison.OrdinalIgnoreCase)) continue;
                    curr = schema;
                    break;
                }
            }

            if (curr == null && createIfNotExists)
            {
                curr = new AppSchema
                {
                    Name = part.ToString(),
                    Container = node.FullName
                };
                curr.SetProperty<Display, LocaleString>(curr.FullName);
                node.Apps = node.Apps != null ? node.Apps.Concat([curr]).ToArray() : [curr];
            }
            node = curr;
        }
        return node;
    }

    /// <summary>
    /// Gets the application field type
    /// </summary>
    internal Type? GetSystemAppFieldType(string appName, string fieldName)
    {
        _appFieldTypes.TryGetValue($"{appName}.{fieldName}", out Type? type);
        return type;
    }

    /// <summary>
    /// Gets the app & field of the given type
    /// </summary>
    public (AppType? App, AppFieldType? Field) GetSystemAppField(Type fieldType)
        => _typeAppFields.TryGetValue(fieldType, out var info) && _apps.TryGetValue(info.App, out var app)
            ? (app, app.GetField(info.Field))
            : (null, null);
    
    /// <summary>
    /// Gets the app & field of the given type
    /// </summary>
    public (AppType? App, AppFieldType? Field) GetSystemAppField<T>() => GetSystemAppField(typeof(T));
    
    #endregion
    
    #region App Types
    
    public readonly AppType RootAppType = new();
    
    #endregion
}