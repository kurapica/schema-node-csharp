using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;
using System.Collections.Concurrent;

namespace SchemaNode.Runtime;

/// <summary>
/// The namespace node
/// </summary>
public class TypeNamespace: AnySchemeType
{
    #region Data

    /// <summary>
    /// The sub schemas of the namespace
    /// </summary>
    public NodeSchema[] Schemas { get; set; } = [];

    #endregion

    #region Ref

    /// <summary>
    /// The Sub namespaces
    /// </summary>
    public ConcurrentDictionary<string, AnySchemeType> SchemaNodes { get; set; } = new ();

    #endregion

    #region Method

    /// <summary>
    /// Load the schema data
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="schema">The schema</param>
    /// <param name="preload">Whether during preload</param>
    public override async Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        Schemas = schema.Schemas?.Select(p => new NodeSchema
        {
            Name = p.Name,
            Type = p.Type,
            Display = p.Display,
            LoadState = p.LoadState,
        }).ToArray() ?? [];

        if (preload)
        {
            if (schema.Schemas == null || schema.Schemas.Length == 0) return;

            // json
            foreach (NodeSchema s in schema.Schemas.Where(s => s.Type == SchemaType.Json))
                await context.GetSchemaNodeAsync(s.Name, preload: true);

            // scalar
            foreach (NodeSchema s in schema.Schemas.Where(s => s.Type == SchemaType.Scalar))
                await context.GetSchemaNodeAsync(s.Name, preload: true);

            // enum
            foreach (NodeSchema s in schema.Schemas.Where(s => s.Type == SchemaType.Enum))
                await context.GetSchemaNodeAsync(s.Name, preload: true);

            // struct
            foreach (NodeSchema s in schema.Schemas.Where(s => s.Type == SchemaType.Struct))
                await context.GetSchemaNodeAsync(s.Name, preload: true);

            // array
            foreach (NodeSchema s in schema.Schemas.Where(s => s.Type == SchemaType.Array))
                await context.GetSchemaNodeAsync(s.Name, preload: true);

            // function
            foreach (NodeSchema s in schema.Schemas.Where(s => s.Type == SchemaType.Func))
                await context.GetSchemaNodeAsync(s.Name, preload: true);
            
            // namespace
            foreach (NodeSchema s in schema.Schemas.Where(s => s.Type == SchemaType.Namespace))
                await context.GetSchemaNodeAsync(s.Name, preload: true);
        }
    }

    /// <summary>
    /// Whether the node is used
    /// </summary>
    public override bool IsUsed => Schemas.Length > 0;

    #endregion

    #region Conversion

    /// <summary>
    /// Convert the node to schema
    /// </summary>
    public static implicit operator NodeSchema?(TypeNamespace? schema)
    {
        if (schema == null) return null;
        return new NodeSchema
        {
            Name = schema.Name,
            Type = schema.Type,
            Display = schema.Display,
            LoadState = schema.LoadState,
            Used = schema.IsUsed,
            HasSchemas = schema.Schemas.Length > 0
        };
    }

    #endregion
}
