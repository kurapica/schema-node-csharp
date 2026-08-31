using SchemaNode.Attribute;
using SchemaNode.Function;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Int;
using SchemaNode.Property.Record;
using SchemaNode.Relation;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
using NodeType = SchemaNode.Property.Core.NodeType;
using ValueSchemaKind = SchemaNode.Property.Record.ValueSchemaKind;

namespace SchemaNode.Schema;

/// <summary>
/// The int schema kind
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_INT, SCHEMA_KIND_ORDER_INT)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_INT, SCHEMA_KIND_ORDER_INT)]
[Meta<ValueSchemaKind>(SCHEMA_KIND_INT, SCHEMA_KIND_ORDER_INT)]
[Meta<NodeType>(typeof(Runtime.IntType))]
[Meta<SchemaUsage>(typeof(IntUsage))]
[Meta<Append>(typeof(EntrySource), typeof(AsSuggest), typeof(Default), typeof(BlackList), typeof(WhiteList), typeof(Unit), typeof(Error), typeof(StackUpLimit), typeof(Valid))]
[Meta<IntValue>]
public sealed class IntKind;

/// <summary>
/// The int define schema
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_INT_DEFINE, SCHEMA_KIND_ORDER_INT)]
[Meta<Append>(typeof(EntrySource), typeof(Unit), typeof(Error), typeof(Valid))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_INT}.schema")]
[Meta<Attach>(SCHEMA_KIND_INT_DEFINE)]
public sealed class IntSchema : ScalarSchema
{
    /// <summary>
    /// The base int schema to inherit from
    /// </summary>
    [Meta<SchemaType>(typeof(IntType))]
    public override string? Base { get; set; }
}

/// <summary>
/// The int usage
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_INT_USAGE, SCHEMA_KIND_ORDER_INT)]
[Meta<Append>(typeof(AsSuggest), typeof(Default), typeof(BlackList), typeof(WhiteList), typeof(Unit), typeof(Error), typeof(StackUpLimit), typeof(Valid))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_INT}.usage")]
[Meta<Attach>(SCHEMA_KIND_INT_USAGE)]
[Relation<WhiteList, Call>(nameof(Default), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(WhiteList)}")]
[Relation<BlackList, Call>(nameof(Default), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(BlackList)}")]
public sealed class IntUsage;

/// <summary>
/// Declare int property for node schema
/// </summary>
[Meta<Alias>(SCHEMA_KIND_INT)]
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_INT}.{SCHEMA_KIND_INT}")]
[Relation<Visible, Call>(SCHEMA_KIND_INT, NS_SYSTEM_LOGIC_EQ, $"@{nameof(NodeSchema.Kind)}", SCHEMA_KIND_INT)]
public sealed class IntProperty : Property<IntSchema>
{
    public override bool Combine(IProperty other, ISchemaRuntime? runtime = null)
    {
        if (other is not StringProperty { Value: { } otherSchema }) return false;
        if (Value is not { } selfSchema)
        {
            SetValue(otherSchema);
            return true;
        }

        selfSchema.CombineProperties(otherSchema, runtime, SCHEMA_KIND_INT);
        SetValue(selfSchema);
        return true;
    }
}

/// <summary>
/// Represents the int scalar type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_INT}.type")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, NODE_SELF, SCHEMA_KIND_INT)]
public class IntType : ValueType;
