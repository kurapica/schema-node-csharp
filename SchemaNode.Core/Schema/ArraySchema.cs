using SchemaNode.Attribute;
using SchemaNode.Property;
using SchemaNode.Property.Array;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Record;
using SchemaNode.Relation;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
using NodeSchemaKind = SchemaNode.Property.Record.NodeSchemaKind;
using NodeType = SchemaNode.Property.Core.NodeType;
using ValueSchemaKind = SchemaNode.Property.Record.ValueSchemaKind;
using RuntimeArrayType = SchemaNode.Runtime.ArrayType;
using SchemaNode.Struct;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/// <summary>
/// The array kind
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_ARRAY, SCHEMA_KIND_ORDER_ARRAY)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_ARRAY, SCHEMA_KIND_ORDER_ARRAY)]
[Meta<ValueSchemaKind>(SCHEMA_KIND_ARRAY, SCHEMA_KIND_ORDER_ARRAY)]
[Meta<NodeType>(typeof(RuntimeArrayType))]
[Meta<SchemaUsage>(typeof(ArrayUsage))]
[Meta<Append>(typeof(Generics), typeof(Relations))]
[Meta<ArrayValue>]
public sealed class ArrayKind;

/// <summary>
/// The array schema
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_ARRAY_DEFINE, SCHEMA_KIND_ORDER_ARRAY)]
[Meta<Append>(typeof(Generics), typeof(Relations))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ARRAY}.schema")]
[Meta<Attach>(SCHEMA_KIND_ARRAY_DEFINE)]
[Meta<EntrySourceProvider>($"{NS_SYSTEM_SCHEMA_REFLECT_ARRAY}.{nameof(Function.Reflect.Array.getaccessentries)}", $"@{nameof(Element)}", NODE_SELF, ENTRY_ROOT)]
[Meta<AccessValueTypeProvider>($"{NS_SYSTEM_SCHEMA_REFLECT_ARRAY}.{nameof(Function.Reflect.Array.getaccessvaluetype)}", $"@{nameof(Element)}", NODE_SELF)]
[Relation<Visible, Call>(nameof(Primary), NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, $"@{nameof(Element)}", SCHEMA_KIND_STRUCT)]
[Relation<EntrySource, Assign>($"{nameof(Primary)}.{ARRAY_ELEMENT}", $"{NS_SYSTEM_SCHEMA_REFLECT_STRUCT}.{nameof(Function.Reflect.Struct.getindexablefields)}", $"@{nameof(Element)}")]
[Relation<Visible, Call>(nameof(Indexes), NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, $"@{nameof(ArraySchema.Element)}", SCHEMA_KIND_STRUCT)]
[Relation<EntrySource, Assign>($"{nameof(Indexes)}.{ARRAY_ELEMENT}.{nameof(DataIndex.Fields)}.{ARRAY_ELEMENT}", $"{NS_SYSTEM_SCHEMA_REFLECT_STRUCT}.{nameof(SchemaNode.Function.Reflect.Struct.getindexablefields)}", $"@{nameof(ArraySchema.Element)}")]
public sealed class ArraySchema: PropertyOwner
{
    /// <summary>
    /// The element type of the array.
    /// </summary>
    [Meta<SchemaType>(typeof(ElementType))]
    public required string Element { get; set; }
}

/// <summary>
/// The array usage
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_ARRAY_USAGE, SCHEMA_KIND_ORDER_ARRAY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ARRAY}.usage")]
[Meta<Attach>(SCHEMA_KIND_ARRAY_USAGE)]
public sealed class ArrayUsage;

/// <summary>
/// Declare array property for node schema
/// </summary>
[Meta<Alias>(SCHEMA_KIND_ARRAY)]
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_ARRAY}.{SCHEMA_KIND_ARRAY}")]
[Relation<Visible, Call>(SCHEMA_KIND_ARRAY, NS_SYSTEM_LOGIC_EQ, $"@{nameof(NodeSchema.Kind)}", SCHEMA_KIND_ARRAY)]
[Relation<Default, Call>($"@{nameof(NodeSchema.Name)}", $"{NS_SYSTEM_SCHEMA_REFLECT_ARRAY}.genarrayname", $"@array.{nameof(ArraySchema.Element)}")]
[Relation<Default, Call>($"@{nameof(Display)}.{nameof(LocaleString.Key)}", $"{NS_SYSTEM_SCHEMA_REFLECT_ARRAY}.genarraydisplay", $"@array.{nameof(ArraySchema.Element)}")]
public sealed class ArrayProperty : Property<ArraySchema>
{
    public override bool Combine(IProperty other, ISchemaRuntime? runtime = null)
    {
        if (other is not ArrayProperty { Value: { } otherSchema }) return false;
        if (Value is not { } selfSchema)
        {
            SetValue(otherSchema);
            return true;
        }
        
        selfSchema.CombineProperties(otherSchema, runtime, SCHEMA_KIND_ARRAY);
        SetValue(selfSchema);
        return true;
    }
}

/// <summary>
/// Represents the array type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ARRAY}.type")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, NODE_SELF, SCHEMA_KIND_ARRAY)]
public class ArrayType: ValueType;

/// <summary>
/// Represents the non-array type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ARRAY}.elementtype")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_ARRAY_ELE, NODE_SELF)]
public class ElementType : ValueType;
