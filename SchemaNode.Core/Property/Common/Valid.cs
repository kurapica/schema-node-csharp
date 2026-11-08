using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;
using ArrayType = SchemaNode.Runtime.ArrayType;

namespace SchemaNode.Property.Common;

/// <summary>
/// The validation property
/// </summary>
[Meta<OfNodeKind>(NODE_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_COMMON}.valid")]
[Meta<Stackable>(true)]
[Meta<PropertyValueType>($"{NS_SYSTEM_SCHEMA_FUNC_CALL}<{NS_SYSTEM_SCHEMA_FUNC}.valid>")]
public class Valid : FuncCallProperty, IConstraintProperty
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
            if (t is null || ReferenceEquals(t, target)) continue;
            target.RecordSubscription(t.Subscribe(_ => Refresh(target)), this);
        }
    }

    /// <inheritdoc/>
    public override void Clear(IValueAccess target) => target.ClearSubscription(this);

    /// <summary>
    /// Refresh the constraint result
    /// </summary>
    private void Refresh(IValueAccess target)
    {
        if (target is not DataNode node || node.Context is not SchemaContext context) return;
        target.RecordConstraint(this, ValidateAsync(context, target).GetAwaiter().GetResult());
    }

    public async Task<bool?> ValidateAsync(SchemaContext context, IValueAccess node, CancellationToken token = default)
    {
        if (node.IsEmpty) return null;
        if (node is not StructNode && node.GetValue<object>() is null) return null;

        FunctionType? func = !string.IsNullOrWhiteSpace(Value?.Func)
            ? await context.GetNodeTypeAsync<FunctionType>(Value.Func, token: token).ConfigureAwait(false)
            : null;
        if (func == null)
        {
            if (!string.IsNullOrWhiteSpace(Value?.Func))
                context.LogError($"Valid property function {Value.Func} is not a function type");
            return null;
        }

        object?[] args = new object[Value!.Args.Length];
        int arrayIndex = -1;
        IValueAccess owner = Source ?? node;
        for (int i = 0; i < args.Length; i++)
        {
            CallArg arg = Value.Args[i];
            FunctionNodeArgument? argInfo = func.Args.ElementAtOrDefault(i) ?? 
                                            (func.Args.LastOrDefault() is { Variadic: true } p  ? p : null);
            if (argInfo == null) return null; // skip if argument info is not found, or args exceed the non-params args but no params defined
            
            if (!string.IsNullOrWhiteSpace(arg.Source))
            {
                var value = owner.GetAccessValue(arg.Source, node);
                args[i] = value;
                if (value is ArrayNode && argInfo.ValueType is not ArrayType)
                {
                    if (arrayIndex == -1) 
                        arrayIndex = i;
                    else
                    {
                        context.LogError($"Multiple array arguments are not supported in validation function: {func.Name}");
                        return null;
                    }
                }
            }
            else
            {
                args[i] = arg.Value;
            }
        }

        try
        {
            if (arrayIndex >= 0)
            {
                ArrayNode arrayNode = (args[arrayIndex] as ArrayNode)!;
                foreach (var dataNode in arrayNode)
                {
                    args[arrayIndex] = dataNode;
                    if (await func.CallAsync<bool?>(context, args).ConfigureAwait(false) == false)
                        return false;
                }

                return true;
            }
            return await func.CallAsync<bool?>(context, args).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            context.LogError(e.Message);
            return null;
        }
    }

    /// <inheritdoc/>
    public override bool Equals(IProperty other)
    {
        if (other is not Valid valid) return false;
        if (HasValue != valid.HasValue) return false;
        if (!HasValue) return true;
        if (Value!.Func != valid.Value!.Func) return false;
        if (Value.Args.Length != valid.Value.Args.Length) return false;
        return !Value.Args.Where((t, i) => !t.Equals(valid.Value.Args[i])).Any();
    }
}