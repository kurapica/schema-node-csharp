using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Function;
using SchemaNode.Property.Core;
using SchemaNode.Property.Struct;
using SchemaNode.Relation;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Common;

/// <summary>
/// The default value
/// </summary>
[Meta<OfNodeKind>(NODE_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_COMMON}.{nameof(Default)}")]
[Relation<OverrideType, Call>(nameof(Default), $"{NS_SYSTEM_SCHEMA_REFLECT_ARRAY}.{nameof(SchemaNode.Function.Reflect.Array.getarrayelement)}", TYPE_PROVIDER)]
public class Default: Property<object>
{
    /// <summary>
    /// The previous value, used to track whether the target value was set by the default
    /// </summary>
    private object? _oldValue;

    /// <inheritdoc/>
    public override void SetValue<TValue>(TValue value)
    {
        _oldValue = GetValue<object>();
        base.SetValue(value);
    }

    /// <inheritdoc/>
    public override void Effect(IValueAccess target)
    {
        object? origin = target.GetValue<object>();
        if (SystemLogic.isempty(origin) || IsEqual(origin, _oldValue))
            target.TrySetValue<object>(Value);
    }

    /// <inheritdoc/>
    public override void Clear(IValueAccess target)
    {
        if (target.GetProperty<DisplayOnly>()?.Value != true) return;
        object? origin = target.GetValue<object>();
        if (!SystemLogic.isempty(_oldValue) && IsEqual(origin, _oldValue))
            target.ClearValue();
    }

    /// <inheritdoc/>
    public override bool InitWithRelation(IRelation relation, IValueAccess owner, IValueAccess target)
    {
        if (target.GetProperty<DisplayOnly>()?.Value == true && !target.IsEmpty)
        {
            target.SetPropertyValue<Default, object>(target.GetValue<object>(), target, null);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Whether the two values are equal
    /// </summary>
    private static bool IsEqual(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;

        JsonNode? nodeA = a.ToJsonNode();
        JsonNode? nodeB = b.ToJsonNode();
        if (nodeA is not null && nodeB is not null)
            return JsonNode.DeepEquals(nodeA, nodeB);
        return a.Equals(b);
    }
}
