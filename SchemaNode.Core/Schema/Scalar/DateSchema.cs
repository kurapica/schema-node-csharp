using SchemaNode.Attribute;
using SchemaNode.Function;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Date;
using SchemaNode.Property.Record;
using SchemaNode.Relation;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
using NodeType = SchemaNode.Property.Core.NodeType;
using ValueSchemaKind = SchemaNode.Property.Record.ValueSchemaKind;

namespace SchemaNode.Schema;

/// <summary>
/// The date schema kind
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_DATE, SCHEMA_KIND_ORDER_DATE)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_DATE, SCHEMA_KIND_ORDER_DATE)]
[Meta<ValueSchemaKind>(SCHEMA_KIND_DATE, SCHEMA_KIND_ORDER_DATE)]
[Meta<NodeType>(typeof(Runtime.DateType))]
[Meta<SchemaUsage>(typeof(DateUsage))]
[Meta<Append>(typeof(AsSuggest), typeof(Default),  typeof(BlackList), typeof(WhiteList), typeof(Error), typeof(Valid))]
[Meta<DateValue>]
public sealed class DateKind;

/// <summary>
/// The date define schema
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_DATE_DEFINE, SCHEMA_KIND_ORDER_DATE)]
[Meta<Append>(typeof(Error), typeof(Valid))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_DATE}.schema")]
[Meta<Attach>(SCHEMA_KIND_DATE_DEFINE)]
public sealed class DateSchema : ScalarSchema
{
    /// <summary>
    /// The base date schema to inherit from
    /// </summary>
    [Meta<SchemaType>(typeof(DateType))]
    public override string? Base { get; set; }
}

/// <summary>
/// The date usage
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_DATE_USAGE, SCHEMA_KIND_ORDER_DATE)]
[Meta<Append>(typeof(AsSuggest), typeof(Default),  typeof(BlackList), typeof(WhiteList), typeof(Error), typeof(Valid))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_DATE}.usage")]
[Meta<Attach>(SCHEMA_KIND_DATE_USAGE)]
[Relation<WhiteList, Call>(nameof(Default), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(WhiteList)}")]
[Relation<BlackList, Call>(nameof(Default), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(BlackList)}")]
public sealed class DateUsage;

/// <summary>
/// Declare date property for node schema
/// </summary>
[Meta<Alias>(SCHEMA_KIND_DATE)]
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_DATE}.{SCHEMA_KIND_DATE}")]
[Relation<Visible, Call>(SCHEMA_KIND_DATE, NS_SYSTEM_LOGIC_EQ, $"@{nameof(NodeSchema.Kind)}", SCHEMA_KIND_DATE)]
public sealed class DateProperty : Property<DateSchema>
{
    public override bool Combine(IProperty other, ISchemaRuntime? runtime = null)
    {
        if (other is not StringProperty { Value: { } otherSchema }) return false;
        if (Value is not { } selfSchema)
        {
            SetValue(otherSchema);
            return true;
        }

        selfSchema.CombineProperties(otherSchema, runtime, SCHEMA_KIND_DATE);
        SetValue(selfSchema);
        return true;
    }
}

/// <summary>
/// Represents the date scalar type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_DATE}.type")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, NODE_SELF, SCHEMA_KIND_DATE)]
public class DateType : ValueType;
