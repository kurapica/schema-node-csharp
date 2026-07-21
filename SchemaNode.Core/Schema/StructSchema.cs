using SchemaNode.Attribute;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Record;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Scalar;
using SchemaNode.Service;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using NodeType = SchemaNode.Property.Core.NodeType;
using RuntimeStructType = SchemaNode.Runtime.StructType;

namespace SchemaNode.Schema;

/// <summary>
/// The struct schema
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_STRUCT, SCHEMA_KIND_ORDER_STRUCT)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_STRUCT, SCHEMA_KIND_ORDER_STRUCT)]
[Meta<ValueSchemaKind>(SCHEMA_KIND_STRUCT, SCHEMA_KIND_ORDER_STRUCT)]
[Meta<NodeType>(typeof(RuntimeStructType))]
[Meta<SchemaGenerator>(typeof(StructGenerator))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_STRUCT}.schema")]
[Meta<Attach>(SCHEMA_KIND_STRUCT)]
[Meta<Append>(typeof(Relations))]
[Meta<Property.Constraint.Struct>]
public sealed class StructSchema : PropertyOwner
{
    /// <summary>
    /// The struct fields
    /// </summary>
    public StructFieldSchema[] Fields { get; set; } = [];
}

/// <summary>
/// Declare struct property for node schema
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.struct")]
[Relation<Visible, Relation.Call>(NODE_SELF, NS_SYSTEM_LOGIC_EQ, $"@{nameof(NodeSchema.Kind)}", SCHEMA_KIND_STRUCT)]
public sealed class StructProperty : Property<StructSchema>
{
    /// <inheritdoc/>
    public override bool Combine(IProperty other, ISchemaRuntime? runtime = null)
    {
        if (other is not StructProperty { Value: {} otherStruct }) return false;
        if (Value is not { } selfStruct)
        {
            SetValue(otherStruct);
            return true;
        }
        selfStruct.CombineProperties(otherStruct, runtime, SCHEMA_KIND_STRUCT);

        // Combine struct fields
        List<StructFieldSchema> combineFields = [];
        HashSet<string> matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < otherStruct.Fields.Length; i++)
        {
            var otherField = otherStruct.Fields[i];
            if (!matched.Add(otherField.Name)) continue;
            
            int index = selfStruct.Fields.FindIndex(f => f.Name.Equals(otherField.Name, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                for (int j = 0; j < index; j++)
                {
                    var existField = selfStruct.Fields[j];
                    if (otherStruct.Fields.All(f => !f.Name.Equals(existField.Name, StringComparison.OrdinalIgnoreCase)) && matched.Add(existField.Name))
                        combineFields.Add(existField);
                }
                combineFields.Add((selfStruct.Fields[index].CombineProperties(otherField, runtime, SCHEMA_KIND_STRUCT_FIELD) as StructFieldSchema)!);
            }
            else
                combineFields.Add(otherField);
        }
        combineFields.AddRange(selfStruct.Fields.Where(field => matched.Add(field.Name)));
        selfStruct.Fields = combineFields.ToArray();
        
        SetValue(selfStruct);
        return true;
    }
}

/// <summary>
/// Represents the struct type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_STRUCT}.type")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, NODE_SELF, SCHEMA_KIND_STRUCT)]
public class StructType: ValueType;

/// <summary>
/// The struct field schema
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_STRUCT}.field")]
[Meta<SchemaKind>(SCHEMA_KIND_STRUCT_FIELD, SCHEMA_KIND_ORDER_STRUCT_FIELD)]
[Meta<Attach>(SCHEMA_KIND_STRUCT_FIELD)]
public sealed class StructFieldSchema : PropertyOwner, IErrorProvider
{
    /// <summary>
    /// The field name
    /// </summary>
    [Meta<SchemaType>(typeof(Identifier))]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The type name of the node.
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// The error status
    /// </summary>
    [Meta<SchemaType>(typeof(Enum.ErrorCode))]
    [Meta<ReadOnly>(true)]
    public string? Error { get; set; }
}