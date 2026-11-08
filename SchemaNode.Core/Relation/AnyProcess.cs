﻿using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Relation;

/// <summary>
/// The relation any check, invoke all the given functions with the same args
/// </summary>
public class AnyProcess : IRelationProcess, INodeReferences, IErrorProvider
{
    public static RelationSchema[] CombineAnyRelations(IEnumerable<RelationSchema> schemas)
    {
        List<RelationSchema> relations = [];

        foreach (RelationSchema schema in schemas)
        {
            var exist = schema.Kind == "call" ? relations.FirstOrDefault(r => r.Target.Equals(schema.Target, StringComparison.OrdinalIgnoreCase) &&
                r.Property.Equals(schema.Property, StringComparison.OrdinalIgnoreCase)) : null;
            if (exist != null)
            {
                if (exist.Kind == "any")
                {
                    var any = exist.GetProperty<Any>()!;
                    any.SetValue(any.GetValue<FuncCall[]>()!.Concat([schema.GetProperty<Call>()!.Value]));
                    exist.SetProperty(any);
                    continue;
                }
                else if(exist.Kind == "call")
                {
                    var replace = new RelationSchema { Kind = "any", Property = exist.Property, Target = exist.Target };
                    var any = new Any();
                    any.SetValue<FuncCall[]>([schema.GetProperty<Call>()!.Value!, exist.GetProperty<Call>()!.Value!]);
                    replace.SetProperty(any);
                    relations.Add(replace);
                    continue;
                }
            }
            relations.Add(schema);
        }
        return relations.ToArray();
    }

    /// <summary>
    /// The load error
    /// </summary>
    public string? Error { get; private set; }

    private FunctionType[]? _funcTypes;
    private FuncCall[]? _calls;

    /// <inheritdoc/>
    public async Task LoadAsync(SchemaContext context, RelationSchema schema, IValueTypeAccess owner, string?[] kinds, CancellationToken token = default)
    {
        FuncCall[]? calls = schema.GetProperty<Any>()?.GetValue<FuncCall[]>();
        if (calls == null || calls.Length == 0) return;
        List<FunctionType> types = [];
        foreach(var call in calls)
        {
            var funcType = await context.GetNodeTypeAsync<FunctionType>(call.Func, null, null, true, token).ConfigureAwait(false);
            if (funcType == null)
            {
                Error ??= ErrorCodes.RELATION_FUNC_NOT_EXIST;
                return;
            }
            types.Add(funcType);
        }
        _calls = calls;
        _funcTypes = types.ToArray();
    }

    /// <inheritdoc/>
    public bool hasDepends => _calls?.Any(c => c.Args?.Any(a => !string.IsNullOrWhiteSpace(a.Source)) ?? false) ?? false;

    /// <inheritdoc/>
    public void Attach(IRelation relation, IValueAccess owner, IValueAccess target)
    {
        if (_calls is null) return;
        foreach (var call in _calls)
        {
            if (call.Args is null) continue;
            foreach (var arg in call.Args)
            {
                if (string.IsNullOrWhiteSpace(arg.Source)) continue;
                IValueAccess? node = owner.GetAccessValue(arg.Source, target);
                if (node == null) continue;
                target.RecordSubscription(node.Subscribe(_ => DebounceProcess(relation, owner, target)), relation);
            }
        }
    }

    /// <inheritdoc/>
    public void Detach(IRelation relation, IValueAccess owner, IValueAccess target)
        => target.ClearSubscription(relation);

    /// <inheritdoc/>
    public async Task<object?> ProcessAsync(SchemaContext context, IValueAccess owner, IValueAccess target)
    {
        if (_funcTypes is null || _funcTypes.Length == 0 || _calls is null) return null;

        // build the args by flattening all call args
        for (int i = 0; i < _funcTypes.Length; i++)
        {
            var args = _calls.ElementAtOrDefault(i)?.Args;
            if (args == null) continue;
            bool? result = await _funcTypes[i].CallAsync<bool?>(context, args.Select(a => {
                if (string.IsNullOrWhiteSpace(a.Source))
                    return a.Value;
                else
                {
                    IValueAccess? access = owner.GetAccessValue(a.Source, target);
                    return access?.GetValue<object>();
                }
            }).ToArray()).ConfigureAwait(false);
            if (result == true) return true;
        }
        return false;
    }

    /// <summary>
    /// Debounce the relation process
    /// </summary>
    private static void DebounceProcess(IRelation relation, IValueAccess owner, IValueAccess target)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(10).ConfigureAwait(false);
                await relation.ProcessAsync(owner, target).ConfigureAwait(false);
            }
            catch
            {
                // ignore the errors, the relation will be processed next time
            }
        });
    }

    /// <inheritdoc/>
    public IEnumerable<Runtime.NodeType> GetReferenceTypes()
    {
        if (_funcTypes is { Length: >0})
            foreach (var type in _funcTypes)
                yield return type;
    }
}

/// <summary>
/// Declare relation call field for the relation
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE_RELATION)]
[Meta<OfNodeKind>(NODE_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_RELATION}.any")]
[Meta<Property.Record.RelationKind>("any", 1)]
[Meta<RelationProcess>(typeof(AnyProcess))]
[Relation<Visible, Call>(nameof(Call), NS_SYSTEM_LOGIC_EQ, $"@{nameof(RelationSchema.Kind)}", "any")]
[Meta<PropertyValueType>($"{NS_SYSTEM_LIST}<{NS_SYSTEM_SCHEMA_FUNC_CALL}<{NS_SYSTEM_SCHEMA_FUNC}.valid>>")]
public class Any : Property<FuncCall[]>;