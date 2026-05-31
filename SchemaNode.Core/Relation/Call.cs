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
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_RELATION}.call")]
public class Call : IRelationProcess, INodeReferences, INodeError
{
    /// <summary>
    /// The function to be used
    /// </summary>
    [Meta<SchemaType>(typeof(FuncType))]
    public string Func { get; set; } = null!;

    /// <summary>
    /// The call arguments
    /// </summary>
    public CallArg[] Args { get; init; } = [];
    
    /// <summary>
    /// The load error
    /// </summary>
    [SchemaIgnore]
    public string? Error { get; private set; }

    private FunctionType? _funType;

    /// <inheritdoc/>
    public async Task LoadAsync(SchemaContext context, Runtime.ValueType valueType)
    {
        _funType = !string.IsNullOrWhiteSpace(Func) ? await context.GetNodeTypeAsync<FunctionType>(Func): null;
        Error = _funType == null ? ErrorCodes.RELATION_FUNC_NOT_EXIST : null;
        
        // check args
        foreach (var arg in Args)
        {
            if (!string.IsNullOrWhiteSpace(arg.Source) && valueType.GetAccessValueType(arg.Source) == null)
                Error ??= ErrorCodes.STRUCT_RELATION_WRONG_ARGS;
        }
    }
    
    /// <inheritdoc/>
    public async Task<object?> ProcessAsync(SchemaContext context, DataNode owner)
    {
        if (_funType == null) return null;
        return await _funType.CallAsync<object?>(context, Args.Select<CallArg, object?>(a =>
        {
            if (string.IsNullOrWhiteSpace(a.Source)) return a.Value;
            DataNode? value = owner.GetAccessValue(a.Source);
            if (value == null) throw new Exception($"Source {a.Source} not found in owner");
            return value;
        }).ToArray());
    }

    /// <inheritdoc/>
    public IEnumerable<Runtime.NodeType> GetReferenceTypes()
    {
        if (_funType is not null)
            yield return _funType;
    }
}

/// <summary>
/// Declare relation call field for the relation
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_RELATION)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_RELATION}.call")]
[Meta<SchemaNode.Property.Record.RelationKind>("call", 1)]
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(RelationSchema.Kind)}", "call")]
public class CallProperty : Property<Call>;
