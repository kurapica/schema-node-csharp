using SchemaNode.Attribute;
using SchemaNode.Function;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Core;
using SchemaNode.Relation;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Common;

/// <summary>
/// The default value
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRUCT_FIELD, SCHEMA_KIND_FUNC_ARG)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_COMMON}.{nameof(Default)}")]
[Relation<Visible, Call>(nameof(Default), NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, $"@{nameof(StructFieldSchema.Type)}", true, SCHEMA_KIND_ENUM, SCHEMA_KIND_BOOL, SCHEMA_KIND_INT, SCHEMA_KIND_DECIMAL, SCHEMA_KIND_STRING, SCHEMA_KIND_DATE)]
[Relation<OverrideType, Call>(nameof(Default), $"{NS_SYSTEM_SCHEMA_REFLECT_ARRAY}.{nameof(SchemaNode.Function.Reflect.Array.getarrayelement)}", $"@{nameof(StructFieldSchema.Type)}")]
[Relation<WhiteList, Call>(nameof(Default), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(WhiteList)}")]
[Relation<BlackList, Call>(nameof(Default), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(BlackList)}")]
[Relation<Root, Call>(nameof(Default), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(Root)}")]
[Relation<LeafOnly, Call>(nameof(Default), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(LeafOnly)}")]
[Relation<Cascade, Call>(nameof(Default), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(Cascade)}")]
[Relation<SingleFlag, Call>(nameof(Default), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(SingleFlag)}")]
public class Default: Property<object>;
