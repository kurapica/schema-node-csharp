using SchemaNode.Attribute;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Record;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Property.Struct;
using SchemaNode.Runtime;
using SchemaNode.Scalar;
using SchemaNode.Service;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using NodeType = SchemaNode.Property.Core.NodeType;
using RuntimeStructType = SchemaNode.Runtime.StructType;

namespace SchemaNode.Schema;

[Meta<SchemaKind>(SCHEMA_KIND_STRUCT, SCHEMA_KIND_ORDER_STRUCT)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_STRUCT, SCHEMA_KIND_ORDER_STRUCT)]
[Meta<ValueSchemaKind>(SCHEMA_KIND_STRUCT, SCHEMA_KIND_ORDER_STRUCT)]
[Meta<NodeType>(typeof(RuntimeStructType))]
[Meta<SchemaGenerator>(typeof(StructGenerator))]
[Meta<SchemaUsage>(typeof(StructUsage))]
[Meta<Append>(typeof(Generics), typeof(Relations), typeof(EntrySourceProvider), typeof(AccessValueTypeProvider), typeof(TypeProvider), typeof(KindProvider), typeof(Valid))]
[Meta<StructValue>]
public sealed class StructKind;

/// <summary>
/// The struct schema
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_STRUCT_DEFINE, SCHEMA_KIND_ORDER_STRUCT)]
[Meta<Append>(typeof(Generics), typeof(Relations), typeof(Valid))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_STRUCT}.schema")]
[Meta<Attach>(SCHEMA_KIND_STRUCT_DEFINE)]
[Meta<EntrySourceProvider>($"{NS_SYSTEM_SCHEMA_REFLECT_STRUCT}.{nameof(Function.Reflect.Struct.getaccessentries)}", $"@{nameof(Fields)}", NODE_SELF)]
[Meta<AccessValueTypeProvider>($"{NS_SYSTEM_SCHEMA_REFLECT_STRUCT}.{nameof(Function.Reflect.Struct.getaccessvaluetype)}", $"@{nameof(Fields)}", NODE_SELF)]
public sealed class StructSchema : PropertyOwner
{
    /// <summary>
    /// The struct fields
    /// </summary>
    public StructFieldSchema[] Fields { get; set; } = [];
}

/// <summary>
/// The struct usage
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_STRUCT_USAGE, SCHEMA_KIND_ORDER_STRUCT)]
[Meta<Append>(typeof(Valid))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_STRUCT}.usage")]
[Meta<Attach>(SCHEMA_KIND_STRUCT_USAGE)]
[Meta<EntrySourceProvider>($"{NS_SYSTEM_SCHEMA_REFLECT_TYPE}.{nameof(Function.Reflect.Type.getaccessentries)}", TYPE_PROVIDER, NODE_SELF)]
[Meta<AccessValueTypeProvider>($"{NS_SYSTEM_SCHEMA_REFLECT_TYPE}.{nameof(Function.Reflect.Type.getaccessvaluetype)}", TYPE_PROVIDER, NODE_SELF)]
public sealed class StructUsage;

/// <summary>
/// Declare struct property for node schema
/// </summary>
[Meta<Alias>(SCHEMA_KIND_STRUCT)]
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_STRUCT}.{SCHEMA_KIND_STRUCT}")]
[Relation<Visible, Relation.Call>(SCHEMA_KIND_STRUCT, NS_SYSTEM_LOGIC_EQ, $"@{nameof(NodeSchema.Kind)}", SCHEMA_KIND_STRUCT)]
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
[Meta<TypeProvider>(nameof(Type))]
[Meta<KindProvider>(SCHEMA_KIND_STRUCT_FIELD)]
[Meta<Append>(typeof(Disable), typeof(Display), typeof(Description), typeof(Visible), typeof(InVisible), 
    typeof(Immutable), typeof(ReadOnly), typeof(Require), typeof(OverrideType))]
public sealed class StructFieldSchema : PropertyOwner, IErrorProvider
{
    /// <summary>
    /// The field name
    /// </summary>
    [Meta<SchemaType>(typeof(Identifier))]
    [Meta<PrimaryIndex>]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The type name of the node.
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// The error status
    /// </summary>
    [SchemaIgnore]
    public string? Error { get; set; }
}