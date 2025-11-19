using SchemaNode.Attribute;
using SchemaNode.Context;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Components.Context;

public class AppAccessContextItemProvider(SchemaContext schemaContext)
    : ISchemaContextItemProvider<AppAccessContextItem>
{
    /// <inheritdoc />
    public bool HasItem => !string.IsNullOrEmpty(schemaContext.App);
    
    /// <inheritdoc />
    public AppAccessContextItem GetItem()
    {
        return HasItem ? new AppAccessContextItem
        {
            App = schemaContext.App!,
            Target = schemaContext.Target,
            Field = schemaContext.Field
        }
        : throw new InvalidOperationException();
    }
}

/// <summary>
/// The application data access context item
/// </summary>
[SchemaType($"{NS_SYSTEM_SCHEMA}.appaccess")]
public class AppAccessContextItem
{
    /// <summary>
    /// The application identifier
    /// </summary>
    public string App { get; set; } = string.Empty;
    
    /// <summary>
    /// The data target
    /// </summary>
    public string? Target { get; set; }
    
    /// <summary>
    /// The access field
    /// </summary>
    public string? Field { get; set; }
}