using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Relation;

/// <summary>
/// The relation call
/// </summary>
public class CallProcess : IRelationProcess, INodeReferences, IErrorProvider
{
    /// <summary>
    /// The function to be used
    /// </summary>
    [Meta<SchemaType>(typeof(FuncType))]
    public string Func { get; private set; } = null!;

    /// <summary>
    /// The call arguments
    /// </summary>
    public CallArg[] Args { get; private set; } = [];
    
    /// <summary>
    /// The load error
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// The function type
    /// </summary>
    public FunctionType? FuncType { get; private set; }

    /// <inheritdoc/>
    public async Task LoadAsync(SchemaContext context, RelationSchema schema, IValueTypeAccess owner)
    {
        FuncCall? call = schema.GetProperty<Call>()?.GetValue<FuncCall>();
        if (call == null)
        {
            Error ??= ErrorCodes.RELATION_FUNC_NOT_EXIST;
            return;
        }
        Func = call.Func;
        Args = call.Args;
        
        // load & check
        FuncType = !string.IsNullOrWhiteSpace(Func) ? await context.GetNodeTypeAsync<FunctionType>(Func): null;
        Error = FuncType == null ? ErrorCodes.RELATION_FUNC_NOT_EXIST : null;
        
        // check args
        foreach (var arg in Args)
        {
            if (!string.IsNullOrWhiteSpace(arg.Source) && owner.GetAccessValueType(arg.Source) == null)
                Error ??= ErrorCodes.STRUCT_RELATION_WRONG_ARGS;
        }
    }
    
    /// <inheritdoc/>
    public async Task<object?> ProcessAsync(SchemaContext context, IValueAccess owner, IValueAccess? target = null)
    {
        if (FuncType == null) return null;
        return await FuncType.CallAsync<object?>(context, Args.Select<CallArg, object?>(a =>
        {
            if (string.IsNullOrWhiteSpace(a.Source)) return a.Value;
            var value = owner.GetAccessValue(a.Source, target);
            return value;
        }).ToArray());
    }

    /// <inheritdoc/>
    public IEnumerable<Runtime.NodeType> GetReferenceTypes()
    {
        if (FuncType is not null)
            yield return FuncType;
    }
}

/// <summary>
/// Declare relation call field for the relation
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_RELATION)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_RELATION}.call")]
[Meta<SchemaNode.Property.Record.RelationKind>("call", 1)]
[Meta<RelationProcess>(typeof(CallProcess))]
[Relation<Visible, Call>(NODE_SELF, NS_SYSTEM_LOGIC_EQ, $"${nameof(RelationSchema.Kind)}", "call")]
[Relation<EntrySource, Call>($"${nameof(CallProcess.Args)}.{nameof(CallArg.Source)}", NS_SYSTEM_SCHEMA_REFLECT_GET_SUB_ENTRIES, RELATION_OWNER, NODE_SELF)]
public class Call : FuncCallProperty;