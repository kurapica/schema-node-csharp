using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Core;
using SchemaNode.Service;
using SchemaNode.Struct;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
using NodeSchemaKind = SchemaNode.Property.Record.NodeSchemaKind;
using ValueSchemaKind = SchemaNode.Property.Record.ValueSchemaKind;
using SchemaKind =  SchemaNode.Property.Record.SchemaKind;
using NodeType = SchemaNode.Property.Core.NodeType;
using SchemaType = SchemaNode.Property.Core.SchemaType;
using RuntimeEnumType = SchemaNode.Runtime.EnumType;
using SchemaNode.Function;
using SchemaNode.Property.Presentation;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/// <summary>
/// The enum schema
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_ENUM, SCHEMA_KIND_ORDER_ENUM)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_ENUM, SCHEMA_KIND_ORDER_ENUM)]
[Meta<ValueSchemaKind>(SCHEMA_KIND_ENUM, SCHEMA_KIND_ORDER_ENUM)]
[Meta<NodeType>(typeof(RuntimeEnumType))]
[Meta<SchemaGenerator>(typeof(EnumGenerator))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ENUM}.schema")]
[Meta<Attach>(SCHEMA_KIND_ENUM)]
[Meta<EnumValue>]
[Meta<EntrySource>($"{NS_SYSTEM_SCHEMA_REFLECT_ENUM}.{nameof(SystemReflect.Enum.getenumaccess)}", NODE_TYPE, NODE_SELF, ENTRY_ROOT)]
[Relation<Immutable, Relation.Assign>($"{nameof(Values)}.{nameof(Entry<string>.Value)}", true)]
public sealed class EnumSchema : PropertyOwner
{
    /// <summary>
    /// The enum value type
    /// </summary>
    public EnumValueType Type { get; set; }
    
    /// <summary>
    /// The cascades of the enum value
    /// </summary>
    [Relation<InVisible, Relation.Call>(NODE_SELF, NS_SYSTEM_LOGIC_EQ, $"@{nameof(Type)}", EnumValueType.Flags)]
    public LocaleString[]? Cascade { get; set; }

    /// <summary>
    /// The enum values
    /// </summary>
    [Relation<OverrideType, Relation.Call>(NODE_SELF, $"{NS_SYSTEM_SCHEMA_REFLECT_ENUM}.{nameof(SystemReflect.Enum.getvaluetype)}", $"@{nameof(Type)}")]
    [Relation<Default, Relation.Call>($"{nameof(Values)}.{nameof(Entry<string>.Value)}", $"{NS_SYSTEM_SCHEMA_REFLECT_ENUM}.{nameof(SystemReflect.Enum.getdefaultentryvalue)}", $"@{nameof(Type)}", $"@{nameof(Values)}.{ARRAY_PREVIOUS}")]
    public Entry<string>[] Values { get; set; } = [];
}

/// <summary>
/// Declare enum property for node schema
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.enum")]
[Relation<Visible, Relation.Call>("enum", NS_SYSTEM_LOGIC_EQ, $"@{nameof(NodeSchema.Kind)}", SCHEMA_KIND_ENUM)]
public sealed class EnumProperty : Property<EnumSchema>
{
    public override bool Combine(IProperty other, ISchemaRuntime? runtime = null)
    {
        if (other is not EnumProperty { Value: {} otherSchema })  return false;
        if (Value is not { } schema)
        {
            SetValue(otherSchema);
            return true;
        }

        if (schema.Cascade is { Length: > 0 })
        {
            for (int i = 0; i < schema.Cascade.Length; i++)
            {
                var cascade = schema.Cascade[i];
                var otherCascade = otherSchema.Cascade?.ElementAtOrDefault(i);
                if (otherCascade is null) break;
                cascade.Concat(otherCascade);
            }
        }
        
        foreach (var value in schema.Values)
        {
            var otherValue = otherSchema.Values?.FirstOrDefault(o => o.Value.Equals(value.Value, StringComparison.OrdinalIgnoreCase));
            if (otherValue is null) break;
            value.CombineProperties(otherValue, runtime, SCHEMA_KIND_ENTRY);
        }

        schema.CombineProperties(otherSchema, runtime, SCHEMA_KIND_ENUM);
        SetValue(schema);
        return true;
    }
    }

/// <summary>
/// Represents the enum type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ENUM}.type")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, NODE_SELF, SCHEMA_KIND_ENUM)]
public class EnumType: ValueType;