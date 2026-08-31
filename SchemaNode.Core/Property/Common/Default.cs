using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Relation;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Common;

/// <summary>
/// The default value
/// </summary>
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_COMMON}.{nameof(Default)}")]
[Relation<OverrideType, Call>(nameof(Default), $"{NS_SYSTEM_SCHEMA_REFLECT_ARRAY}.{nameof(SchemaNode.Function.Reflect.Array.getarrayelement)}", NODE_TYPE)]
public class Default: Property<object>;
