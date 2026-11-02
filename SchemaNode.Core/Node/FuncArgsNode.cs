﻿using SchemaNode.Context;
using SchemaNode.Property.Array;
using SchemaNode.Property.Common;
using SchemaNode.Property.Function;
using SchemaNode.Runtime;

namespace SchemaNode.Node;

/// <summary>
/// The function arguments node, the variadic argument is always the last one
/// </summary>
public class FuncArgsNode : ArrayNode
{
    /// <summary>
    /// Whether the variadic argument is shown
    /// </summary>
    private bool _showVariadic = true;

    public FuncArgsNode(ISchemaContext context, IValueTypeAccess type, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders)
        : base(context, type, value, parent, propertyProviders)
    {
        // variadic arguments are the last
        if (FullMode)
            RecordSubscription(SubscribeItem(_ => RefreshVariadic(), true), this);
    }

    /// <summary>
    /// Refresh the variadic argument visibility
    /// </summary>
    private void RefreshVariadic()
    {
        if (!FullMode) return;

        for (int i = 0; i < Count - 1; i++)
        {
            if (this[i] is not IValueAccess arg) continue;
            arg.SetPropertyValue<Variadic, bool>(false, this, null);
            arg.SetPropertyValue<InVisible, bool>(false, this, null);
        }

        if (Count > 0 && this[Count - 1] is IValueAccess last)
            last.SetPropertyValue<InVisible, bool>(!_showVariadic, this, null);
    }

    /// <summary>
    /// Sets whether the variadic argument is shown
    /// </summary>
    public void SetShowVariadic(bool value)
    {
        _showVariadic = value;
        if (!FullMode || Count == 0) return;
        if (this[Count - 1] is IValueAccess last)
            last.SetPropertyValue<InVisible, bool>(!value, this, null);
    }

    /// <summary>
    /// Sets the arguments unmodifiable, all arguments are read only and the size is fixed
    /// </summary>
    public void SetUnModifiable(bool value)
    {
        if (!FullMode) return;

        long size = value ? Count : 0;
        SetPropertyValue<MinSize, long>(size, this, null);
        SetPropertyValue<MaxSize, long>(size, this, null);

        foreach (var arg in this.OfType<FuncArgNode>())
            arg.SetUnModifiable(value);
    }
}
