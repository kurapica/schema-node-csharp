using SchemaNode.Attribute;
using SchemaNode.Function;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Enum;
using SchemaNode.Property.Record;
using SchemaNode.Property.String;
using SchemaNode.Property.Struct;
using SchemaNode.Property.Property;
using SchemaNode.Relation;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
using NodeType = SchemaNode.Property.Core.NodeType;
using ValueSchemaKind = SchemaNode.Property.Record.ValueSchemaKind;

namespace SchemaNode.Schema;

/// <summary>
/// The string schema kind
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_STRING, SCHEMA_KIND_ORDER_STRING)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_STRING, SCHEMA_KIND_ORDER_STRING)]
[Meta<ValueSchemaKind>(SCHEMA_KIND_STRING, SCHEMA_KIND_ORDER_STRING)]
[Meta<NodeType>(typeof(Runtime.StringType))]
[Meta<SchemaUsage>(typeof(StringUsage))]
[Meta<Append>(typeof(EntrySource), typeof(AsSuggest), typeof(Default), typeof(BlackList), typeof(WhiteList), typeof(Root), typeof(LeafOnly), typeof(Unit), typeof(Error), typeof(Valid))]
[Meta<StringValue>]
public sealed class StringKind;

/// <summary>
/// The string define schema
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_STRING_DEFINE, SCHEMA_KIND_ORDER_STRING)]
[Meta<Append>(typeof(EntrySource), typeof(Unit), typeof(Error), typeof(Valid))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_STRING}.schema")]
[Meta<Attach>(SCHEMA_KIND_STRING_DEFINE)]
public sealed class StringSchema : ScalarSchema
{
    /// <summary>
    /// The base string schema to inherit from
    /// </summary>
    [Meta<SchemaType>(typeof(StringType))]
    public override string? Base { get; set; }
}

/// <summary>
/// The string use settings
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_STRING_USAGE, SCHEMA_KIND_ORDER_STRING)]
[Meta<Append>(typeof(AsSuggest), typeof(Default), typeof(BlackList), typeof(WhiteList), typeof(Root), typeof(LeafOnly), typeof(Unit), typeof(Error), typeof(Valid))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_STRING}.usage")]
[Meta<Attach>(SCHEMA_KIND_STRING_USAGE)]
[Relation<WhiteList, Call>(nameof(Default), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(WhiteList)}")]
[Relation<BlackList, Call>(nameof(Default), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(BlackList)}")]
[Relation<Root, Call>(nameof(Default), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(Root)}")]
[Relation<LeafOnly, Call>(nameof(Default), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(LeafOnly)}")]
[Relation<Root, Call>(nameof(BlackList), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(Root)}")]
[Relation<Root, Call>(nameof(WhiteList), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(Root)}")]
[Relation<BlackList, Call>(nameof(WhiteList), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(BlackList)}")]
[Relation<Visible, Call>(nameof(Root), $"{NS_SYSTEM_SCHEMA_REFLECT_ENUM}.{nameof(Function.Reflect.Enum.hascascade)}", TYPE_PROVIDER)]
[Relation<OverrideType, Call>(nameof(Root), $"{NS_SYSTEM_SCHEMA_REFLECT_ARRAY}.{nameof(Function.Reflect.Array.getarrayelement)}", TYPE_PROVIDER)]
[Relation<Visible, Call>(nameof(LeafOnly), $"{NS_SYSTEM_SCHEMA_REFLECT_ENUM}.{nameof(Function.Reflect.Enum.hascascade)}", TYPE_PROVIDER)]
public sealed class StringUsage;

/// <summary>
/// Declare string property for node schema
/// </summary>
[Meta<Alias>(SCHEMA_KIND_STRING)]
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_STRING}.{SCHEMA_KIND_STRING}")]
[Relation<Visible, Call>(SCHEMA_KIND_STRING, NS_SYSTEM_LOGIC_EQ, $"@{nameof(NodeSchema.Kind)}", SCHEMA_KIND_STRING)]
public sealed class StringProperty : Property<StringSchema>
{
    public override bool Combine(IProperty other, ISchemaRuntime? runtime = null)
    {
        if (other is not StringProperty { Value: { } otherSchema }) return false;
        if (Value is not { } selfSchema)
        {
            SetValue(otherSchema);
            return true;
        }

        selfSchema.CombineProperties(otherSchema, runtime, SCHEMA_KIND_STRING);
        SetValue(selfSchema);
        return true;
    }
}

/// <summary>
/// Represents the string scalar type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_STRING}.type")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, NODE_SELF, SCHEMA_KIND_STRING)]
public class StringType : ValueType;
