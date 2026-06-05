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
                    // Intermediate namespace: create it
                    node = new AppSchema
                    {
                        Name = part,
                        Parent = container
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
    /// Gets system app schema
    /// </summary>
    internal AppSchema? GetSystemAppSchema(string name)
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
                    if (!part.Equals(schema.Name, StringComparison.OrdinalIgnoreCase)) continue;
                    curr = schema;
                    break;
                }
            }
            node = curr;
        }
        return node;
    }

    #endregion
    
    #region App Types
    
    public readonly AppType RootAppType = new();
    
    #endregion
}