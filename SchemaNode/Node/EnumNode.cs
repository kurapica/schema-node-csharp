using System.Text.Json.Nodes;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Node;

/// <summary>
/// The in-memory enum schema representation
/// </summary>
public class EnumNode: NamespaceNode
{
    #region Data
    
    /// <summary>
    /// The enum value type
    /// </summary>
    public EnumValueType ValueType { get; set; } = EnumValueType.String;

    /// <summary>
    /// The cascade list
    /// </summary>
    public string[]? Cascade { get; set; }
    
    /// <summary>
    /// The enum values
    /// </summary>
    public EnumValueInfo[]? Values { get; set; }

    #endregion
    
    #region Status

    /// <inheritdoc />
    public override SchemaType Type => SchemaType.Enum;

    #endregion
    
    #region Method

    /// <inheritdoc />
    public override Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        EnumSchema? @enum = schema.Enum;
        
        // Data
        ValueType = @enum?.Type ?? EnumValueType.String;
        Cascade = @enum?.Cascade;
        Values = @enum?.Values;
        
        // Status
        if (@enum == null) Status = SchemaNodeStatus.NoDefinition;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task<(JsonNode? value, JsonNode? error)> ValidateValueAsync(SchemaContext context, JsonNode value)
    {
        await Task.Yield();
        if (value is not JsonValue val || val.IsEmpty())
            return (value, TYPE_VALUE_NOT_VALID);
        
        
    }

    /// <inheritdoc />
    public override bool CanBeUseAs(NamespaceNode other) => 
        base.CanBeUseAs(other) ||
        other switch
        {
            ScalarNode scalar => ValueType switch
            {
                EnumValueType.String => scalar.IsString,
                EnumValueType.Int => scalar.IsInt,
                EnumValueType.Float => scalar.IsNumber,
                EnumValueType.Double => scalar.IsNumber,
                EnumValueType.Flags => scalar.IsInt,
                _ => false
            },
            _ => false
        };

    /// <inheritdoc />
    public override bool IsIndexable => ValueType is EnumValueType.String or EnumValueType.Int or EnumValueType.Float;
    
    #endregion
}