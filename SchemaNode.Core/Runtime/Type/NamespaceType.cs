using System.Collections.Concurrent;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property.Schema;
using SchemaNode.Schema;

namespace SchemaNode.Runtime;

/// <summary>
/// The namespace node that holds sub-schema types
/// </summary>
public class NamespaceType : AnySchemaType
{
    #region Data

    /// <summary>
    /// The sub schemas of the namespace
    /// </summary>
    public NodeSchema[] Schemas { get; set; } = [];

    #endregion

    #region Ref

    /// <summary>
    /// The sub schema type nodes
    /// </summary>
    public ConcurrentDictionary<string, AnySchemaType> SchemaNodes { get; set; } = new();

    #endregion

    #region Status

    /// <summary>
    /// A namespace is considered "used" if it has schemas
    /// </summary>
    public override bool IsUsed => Schemas.Length > 0;

    #endregion

    #region Loading

    /// <summary>
    /// Load the namespace schema data
    /// </summary>
    public override async Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        // Store sub-schema metadata (name + kind) for lazy loading
        Schemas = schema.Schemas?.Select(p => new NodeSchema
        {
            Name = p.Name,
            Kind = p.Kind,
        }).ToArray() ?? [];

        if (!preload || schema.Schemas == null || schema.Schemas.Length == 0) return;

        ISchemaRuntime runtime = context.Runtime;

        // Preload schemas by kind in dependency order
        foreach (SchemaKindInfo kindInfo in runtime.GetSchemaKinds())
        {
            foreach (NodeSchema s in schema.Schemas.Where(s =>
                         s.Kind?.Equals(kindInfo.Kind, StringComparison.OrdinalIgnoreCase) == true))
            {
                await runtime.GetSchemaTypeAsync(context, s.Name, preload: true);
            }
        }
    }

    #endregion
}