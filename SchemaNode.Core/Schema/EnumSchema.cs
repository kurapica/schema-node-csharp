using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Enum;
using SchemaNode.Relation;
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

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/// <summary>
/// The enum kind
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_ENUM, SCHEMA_KIND_ORDER_ENUM)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_ENUM, SCHEMA_KIND_ORDER_ENUM)]
[Meta<ValueSchemaKind>(SCHEMA_KIND_ENUM, SCHEMA_KIND_ORDER_ENUM)]
[Meta<NodeType>(typeof(RuntimeEnumType))]
[Meta<SchemaUsage>(typeof(EnumUsage))]
[Meta<SchemaGenerator>(typeof(EnumGenerator))]
[Meta<Append>(typeof(EntrySource), typeof(Default), typeof(BlackList), typeof(WhiteList), typeof(Valid))]
[Meta<EnumValue>]
[Meta<EntrySource>($"{NS_SYSTEM_SCHEMA_REFLECT_ENUM}.{nameof(Function.Reflect.Enum.getenumaccess)}", NODE_TYPE, NODE_SELF, ENTRY_ROOT)]
public sealed class EnumKind;

/// <summary>
/// The enum schema
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_ENUM_DEFINE, SCHEMA_KIND_ORDER_ENUM)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ENUM}.schema")]
[Meta<Attach>(SCHEMA_KIND_ENUM_DEFINE)]
public sealed class EnumSchema : PropertyOwner
{
    /// <summary>
    /// The enum value type
    /// </summary>
    public EnumValueType Type { get; set; }
    
    /// <summary>
    /// The cascades of the enum value
    /// </summary>
    [Relation<InVisible, Call>(NODE_SELF, NS_SYSTEM_LOGIC_EQ, $"@{nameof(Type)}", EnumValueType.Flags)]
    public LocaleString[]? Cascade { get; set; }

    /// <summary>
    /// The enum values
    /// </summary>
    [Relation<Immutable, Assign>($"{nameof(Values)}.{ARRAY_ELEMENT}.{nameof(Entry<string>.Value)}", true)]
    [Relation<OverrideType, Call>($"{nameof(Values)}.{ARRAY_ELEMENT}.{nameof(Entry<string>.Value)}", $"{NS_SYSTEM_SCHEMA_REFLECT_ENUM}.{nameof(Function.Reflect.Enum.getvaluetype)}", $"@{nameof(Type)}")]
    [Relation<Default, Call>($"{nameof(Values)}.{ARRAY_ELEMENT}.{nameof(Entry<string>.Value)}", $"{NS_SYSTEM_SCHEMA_REFLECT_ENUM}.{nameof(Function.Reflect.Enum.getdefaultentryvalue)}", $"@{nameof(Type)}", $"@{nameof(Values)}.{ARRAY_PREVIOUS}")]
    public Entry<string>[] Values { get; set; } = [];
}

/// <summary>
/// The enum use setting
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_ENUM_USAGE, SCHEMA_KIND_ORDER_ENUM)]
[Meta<Append>(typeof(Default), typeof(BlackList), typeof(WhiteList), typeof(Valid))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ENUM}.usage")]
[Meta<Attach>(SCHEMA_KIND_ENUM_USAGE)]
[Relation<WhiteList, Call>(nameof(Default), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(WhiteList)}")]
[Relation<BlackList, Call>(nameof(Default), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(BlackList)}")]
[Relation<Root, Call>(nameof(Default), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(Root)}")]
[Relation<LeafOnly, Call>(nameof(Default), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(LeafOnly)}")]
[Relation<Cascade, Call>(nameof(Default), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(Cascade)}")]
[Relation<SingleFlag, Call>(nameof(Default), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(SingleFlag)}")]
[Relation<Root, Call>(nameof(BlackList), $"{NS_SYSTEM_INTRINSIC}.assign", $"@{nameof(Root)}")]
[Relation<Cascade, Call>(nameof(BlackList), $"{NS_SYSTEM_INTRINSIC}.assign", $"@{nameof(Cascade)}")]
[Relation<Root, Call>(nameof(WhiteList), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(Root)}")]
[Relation<Cascade, Call>(nameof(WhiteList), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(Cascade)}")]
[Relation<Visible, Call>(nameof(Root), $"{NS_SYSTEM_SCHEMA_REFLECT_ENUM}.{nameof(Function.Reflect.Enum.hascascade)}", NODE_TYPE)]
[Relation<OverrideType, Call>(nameof(Root), $"{NS_SYSTEM_SCHEMA_REFLECT_ARRAY}.{nameof(Function.Reflect.Array.getarrayelement)}", NODE_TYPE)]
[Relation<Cascade, Call>(nameof(Root), $"{NS_SYSTEM_MATH}.{nameof(SystemMath.subtract)}", $"@{nameof(Cascade)}", 1L)]
[Relation<Visible, Call>(nameof(LeafOnly), $"{NS_SYSTEM_SCHEMA_REFLECT_ENUM}.{nameof(Function.Reflect.Enum.hascascade)}", NODE_TYPE)]
[Relation<EntrySource, Assign>(nameof(Cascade), $"{NS_SYSTEM_SCHEMA_REFLECT_ENUM}.{nameof(Function.Reflect.Enum.getcascades)}", NODE_TYPE)]
public sealed class EnumUsage;

/// <summary>
/// Declare enum property for node schema
/// </summary>
[Meta<Alias>(SCHEMA_KIND_ENUM)]
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_ENUM}.{SCHEMA_KIND_ENUM}")]
[Relation<Visible, Call>(SCHEMA_KIND_ENUM, NS_SYSTEM_LOGIC_EQ, $"@{nameof(NodeSchema.Kind)}", SCHEMA_KIND_ENUM)]
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