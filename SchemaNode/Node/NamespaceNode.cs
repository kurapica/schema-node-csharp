using System.Collections.Concurrent;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;

namespace SchemaNode.Node;

/// <summary>
/// The in-memory schema representation
/// </summary>
public class NamespaceNode
{
    #region Data
    
    /// <summary>
    /// The namespace
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The schema display
    /// </summary>
    public string? Display { get; set; }

    #endregion
    
    #region Status
    
    /// <summary>
    /// The schema type
    /// </summary>
    public virtual SchemaType Type => SchemaType.Namespace;
    
    /// <summary>
    /// The Sub namespaces
    /// </summary>
    public ConcurrentDictionary<string, NamespaceNode>? Schemas { get; set; }

    /// <summary>
    /// Used by other types
    /// </summary>
    protected ConcurrentDictionary<NamespaceNode, bool> UsedBy { get; set; } = new();

    #endregion
    
    #region Methods

    public virtual async Task LoadAsync(SchemaContext context, NodeSchema schema)
    {
        Schemas ??= new ConcurrentDictionary<string, NamespaceNode>();

        if (SchemaContext.Config.PreLoad)
        {
            // try load the sub-schemas
            if (schema.Schemas == null || schema.Schemas.Length == 0)
                schema = await context.SchemaProvider.LoadSchema(schema.Name);
            if (schema?.Schemas == null || schema.Schemas.Length == 0)
                return;

            foreach (NodeSchema s in schema.Schemas.Where(s => s.Type == SchemaType.Scalar))
            {
            }
        }
    }
    
    #endregion
    
    #region Conversion

    /// <summary>
    /// Convert the schema to node
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static implicit operator NamespaceNode?(NodeSchema? schema)
    {
        if (schema == null) return null;
        return schema.Type switch
        {
            SchemaType.Namespace => new NamespaceNode { Name = schema.Name, Display = schema.Display },
            SchemaType.Scalar => new ScalarNode { Name = schema.Name, Display = schema.Display },
            SchemaType.Enum => new EnumNode { Name = schema.Name, Display = schema.Display },
            SchemaType.Struct => new StructNode { Name = schema.Name, Display = schema.Display },
            SchemaType.Array => new ArrayNode { Name = schema.Name, Display = schema.Display },
            SchemaType.Function => new FunctionNode { Name = schema.Name, Display = schema.Display },
            _ => throw new ArgumentOutOfRangeException()
        };
    }
    
    #endregion
}