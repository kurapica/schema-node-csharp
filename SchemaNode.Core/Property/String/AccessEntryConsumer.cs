using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;
using ValueType = SchemaNode.Runtime.ValueType;

namespace SchemaNode.Property.String;

/// <summary>
/// The access value type consumer for ancestor provider
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE_STRING)]
[Meta<OfNodeKind>(NODE_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_STRING}.{nameof(AccessEntryConsumer)}")]
[Meta<Static>(true)]
[Meta<ReadOnly>(true)]
[Meta<InVisible>(true)]
[Meta<PropertyValueType>($"{NS_SYSTEM_SCHEMA_FUNC_CALL}<{NS_SYSTEM_SCHEMA_FUNC}.valid>")]
public class AccessEntryConsumer: FuncCallProperty, IConstraintProperty
{
    /// <inheritdoc/>
    public override void Effect(IValueAccess target)
    {
        Clear(target);

        if (Value?.Args is not { Length: > 0 }) return;
        IValueAccess source = Source ?? target;
        foreach (CallArg arg in Value.Args)
        {
            if (string.IsNullOrWhiteSpace(arg.Source)) continue;
            IValueAccess? t = source.GetAccessValue(arg.Source, target);
            if (t is not null && !ReferenceEquals(t, target))
                target.RecordSubscription(t.Subscribe(_ => Refresh(target)), this);
        }
    }

    /// <inheritdoc/>
    public override void Clear(IValueAccess target) => target.ClearSubscription(this);

    /// <inheritdoc/>
    public virtual async Task<bool?> ValidateAsync(SchemaContext context, IValueAccess node, CancellationToken token = default)
    {
        if (node.IsEmpty || string.IsNullOrWhiteSpace(Value?.Func)) return null;

        // gets the access value type provider
        IValueTypeAccess? valueType = null;
        IValueAccess? parent = node;
        while (parent is not null)
        {
            AccessValueTypeProvider? provider = parent.GetProperty<AccessValueTypeProvider>();
            if (provider is { HasValue: true, Value: { } providerCall } && !string.IsNullOrWhiteSpace(providerCall.Func))
            {
                FunctionType? typeFunc = await context.GetNodeTypeAsync<FunctionType>(providerCall.Func, token: token).ConfigureAwait(false);
                object?[] typeArgs = providerCall.Args.Select(a => string.IsNullOrWhiteSpace(a.Source)
                    ? a.Value
                    : (provider.Source ?? node).GetAccessValue(a.Source, node)?.GetValue<object>()).ToArray();
                string? type = typeFunc is null ? null : await typeFunc.CallAsync<string>(context, typeArgs, token: token).ConfigureAwait(false);
                valueType = !string.IsNullOrWhiteSpace(type) ? await context.GetNodeTypeAsync<ValueType>(type, token: token).ConfigureAwait(false) : null;
                break;
            }
            parent = parent.Parent;
        }
        if (valueType is null) return null;

        FunctionType? func = await context.GetNodeTypeAsync<FunctionType>(Value!.Func, token: token).ConfigureAwait(false);
        if (func is null)
        {
            context.LogError($"Valid property function {Value!.Func} is not a function type");
            return null;
        }

        IValueAccess owner = Source ?? node;
        try
        {
            object?[] args = Value!.Args.Select(a =>
            {
                if (string.IsNullOrWhiteSpace(a.Source)) return a.Value;
                IValueAccess? source = owner.GetAccessValue(a.Source, node);
                return ReferenceEquals(source, node) ? valueType.Name : source?.GetValue<object>();
            }).ToArray();
            return await func.CallAsync<bool?>(context, args, token: token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            context.LogError($"[AccessEntryConsumer] {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Refresh the constraint result
    /// </summary>
    private void Refresh(IValueAccess target)
    {
        if (target is not DataNode node || node.Context is not SchemaContext context) return;
        target.RecordConstraint(this, ValidateAsync(context, target).ConfigureAwait(false).GetAwaiter().GetResult());
    }
}
