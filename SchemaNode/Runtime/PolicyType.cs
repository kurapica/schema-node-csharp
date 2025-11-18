using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory policy schema representation
/// </summary>
public class PolicyType: AnySchemeType
{
    #region Data

    /// <summary>
    /// The policy items
    /// </summary>
    public PolicyItem[] Items { get; set; } = [];

    #endregion
    
    #region Status
    
    /// <inheritdoc />
    public override SchemaType Type => SchemaType.Workflow;
    
    #endregion
    
    #region Method
    
    /// <inheritdoc />
    public override Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        PolicySchema? policy = schema.Policy;
        
        // Data
        Items = policy?.Items ?? [];

        if (policy == null) Status = SchemaNodeStatus.NoDefinition;

        return Task.CompletedTask;
    }
    
    /// <inheritdoc />
    public override ArrayType? GetArrayNode(bool exactly = false)
    {
        return null;
    }

    #endregion

    #region Conversion

    /// <summary>
    /// Convert the node to schema
    /// </summary>
    public static implicit operator NodeSchema?(PolicyType? schema)
    {
        if (schema == null) return null;
        return new NodeSchema
        {
            Name = schema.Name,
            Type = schema.Type,
            Display = schema.Display,
            LoadState = schema.LoadState,
            Auth = schema.Auth?.Name,
            Used = schema.IsUsed,
            Policy = new PolicySchema
            {
                Items = schema.Items.ToArray()
            }
        };
    }
    #endregion
}