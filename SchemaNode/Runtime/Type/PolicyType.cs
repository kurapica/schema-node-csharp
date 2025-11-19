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
    public PolicyItem[] Items { get; private set; } = [];

    #endregion
    
    #region Status
    
    /// <inheritdoc />
    public override SchemaType Type => SchemaType.Policy;
    
    #endregion
    
    #region Method
    
    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        PolicySchema? policy = schema.Policy;
        
        // Data
        Items = policy?.Items ?? [];
        if (policy == null) Status = SchemaNodeStatus.NoDefinition;

        // Ref
        foreach (PolicyItem item in Items)
        {
            FunctionType? func = !string.IsNullOrEmpty(item.Evaluator)
                ? await context.GetSchemaTypeAsync(item.Evaluator) as FunctionType
                : null;
            if (func == null)
            {
                Status = SchemaNodeStatus.PolicyWrongFunc;
            }
            else
            {
                func.AddRef(this);
                item.Function = func;
            }
        }
    }
    
    /// <inheritdoc />
    public override ArrayType? GetArrayNode(bool exactly = false)
    {
        return null;
    }
    
    /// <inheritdoc />
    public override void Release()
    {
        foreach (PolicyItem item in Items)
        {
            item.Function?.RemoveRef(this);
            item.Function = null;
        }
    }

    #endregion

    #region Conversion

    /// <summary>
    /// Convert the node to schema
    /// </summary>
    public static implicit operator NodeSchema?(PolicyType? schema)
    {
        return schema?.ToSchema().With(new PolicySchema
        {
            Items = schema.Items.ToArray()
        });
    }
    #endregion
}