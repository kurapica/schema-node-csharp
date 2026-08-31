using SchemaNode.Attribute;
using SchemaNode.Function;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Decimal;
using SchemaNode.Property.Record;
using SchemaNode.Relation;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
using NodeType = SchemaNode.Property.Core.NodeType;
using ValueSchemaKind = SchemaNode.Property.Record.ValueSchemaKind;

namespace SchemaNode.Schema;

/// <summary>
/// The decimal kind
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_DECIMAL, SCHEMA_KIND_ORDER_DECIMAL)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_DECIMAL, SCHEMA_KIND_ORDER_DECIMAL)]
[Meta<ValueSchemaKind>(SCHEMA_KIND_DECIMAL, SCHEMA_KIND_ORDER_DECIMAL)]
[Meta<NodeType>(typeof(Runtime.DecimalType))]
[Meta<SchemaUsage>(typeof(DecimalUsage))]
[Meta<Append>(typeof(EntrySource), typeof(AsSuggest), typeof(Default), typeof(BlackList), typeof(WhiteList), typeof(Unit), typeof(Error), typeof(StackUpLimit), typeof(Valid))]
[Meta<DecimalValue>]
public sealed class DecimalKind;

/// <summary>
/// The decimal definition
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_DECIMAL_DEFINE, SCHEMA_KIND_ORDER_DECIMAL)]
[Meta<Append>(typeof(EntrySource), typeof(Unit), typeof(Error), typeof(Valid))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_DECIMAL}.schema")]
[Meta<Attach>(SCHEMA_KIND_DECIMAL_DEFINE)]
public sealed class DecimalSchema : ScalarSchema
{
    /// <summary>
    /// The base decimal schema to inherit from
    /// </summary>
    [Meta<SchemaType>(typeof(DecimalType))]
    public override string? Base { get; set; }
}

/// <summary>
/// The decimal usage
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_DECIMAL_USAGE, SCHEMA_KIND_ORDER_DECIMAL)]
[Meta<Append>(typeof(AsSuggest), typeof(Default), typeof(BlackList), typeof(WhiteList), typeof(Unit), typeof(Error), typeof(StackUpLimit), typeof(Valid))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_DECIMAL}.usage")]
[Meta<Attach>(SCHEMA_KIND_DECIMAL_USAGE)]
[Relation<WhiteList, Call>(nameof(Default), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(WhiteList)}")]
[Relation<BlackList, Call>(nameof(Default), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(BlackList)}")]
public sealed class DecimalUsage;

/// <summary>
/// Declare decimal property for node schema
/// </summary>
[Meta<Alias>(SCHEMA_KIND_DECIMAL)]
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_DECIMAL}.{SCHEMA_KIND_DECIMAL}")]
[Relation<Visible, Call>(SCHEMA_KIND_DECIMAL, NS_SYSTEM_LOGIC_EQ, $"@{nameof(NodeSchema.Kind)}", SCHEMA_KIND_DECIMAL)]
public sealed class DecimalProperty : Property<DecimalSchema>
{
    public override bool Combine(IProperty other, ISchemaRuntime? runtime = null)
    {
        if (other is not StringProperty { Value: { } otherSchema }) return false;
        if (Value is not { } selfSchema)
        {
            SetValue(otherSchema);
            return true;
        }

        selfSchema.CombineProperties(otherSchema, runtime, SCHEMA_KIND_DECIMAL);
        SetValue(selfSchema);
        return true;
    }
}

/// <summary>
/// Represents the decimal scalar type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_DECIMAL}.type")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, NODE_SELF, SCHEMA_KIND_DECIMAL)]
public class DecimalType : ValueType;
