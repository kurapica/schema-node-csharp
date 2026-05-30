using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using RelationKind = SchemaNode.Enum.RelationKind;
using SchemaKind =  SchemaNode.Property.Record.SchemaKind;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/// <summary>
/// The relation schemas
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_RELATION}.schema")]
[Meta<SchemaKind>(SCHEMA_KIND_RELATION, SCHEMA_KIND_ORDER_RELATION)]
public class RelationSchema : ExtensibleSchema
{
    /// <summary>
    /// The target of the relation
    /// </summary>
    public string Target { get; set; } = null!;

    /// <summary>
    /// The property the relation applied to
    /// </summary>
    [Meta<SchemaType>(typeof(PropertyType))]
    public string Property { get; set; } = null!;

    /// <summary>
    /// The stage of the relation been applied
    /// </summary>
    public RelationStage Stage { get; set; } = RelationStage.Load | RelationStage.Input;

    /// <summary>
    /// The relation kind
    /// </summary>
    [Meta<SchemaType>(typeof(RelationKind))]
    public string Kind { get; set; } = null!;

    /// <summary>
    /// Equals check
    /// </summary>
    public override bool Equals(ExtensibleSchema? other)
    {
        if (other is not RelationSchema otherRelation) return false;
        if (ReferenceEquals(this, otherRelation)) return true;
        return Target.Equals(otherRelation.Target, StringComparison.OrdinalIgnoreCase) && 
               Property.Equals(otherRelation.Property, StringComparison.OrdinalIgnoreCase) && 
               Stage == otherRelation.Stage && 
               Kind.Equals(otherRelation.Kind, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// The handler to process the relation
/// </summary>
public interface IRelationProcess
{
    Task LoadAsync(SchemaContext context, Runtime.ValueType valueType);
    
    /// <summary>
    /// Process the relation and return the new property value
    /// </summary>
    Task<object?> ProcessAsync(SchemaContext context, DataNode owner);
}

/// <summary>
/// The relation property for data schemas
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRUCT, SCHEMA_KIND_ARRAY, SCHEMA_KIND_PROPERTY)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.relations")]
public class Relations: Property<RelationSchema[]>;

#region Relation Assign

/// <summary>
/// The relation assign
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_RELATION}.assign")]
public class RelationAssign : IRelationProcess
{
    /// <summary>
    /// The value assign to property
    /// </summary>
    public object? Value { get; set; }

    /// <inheritdoc/> 
    public Task LoadAsync(SchemaContext context, Runtime.ValueType valueType) => Task.CompletedTask;

    /// <inheritdoc/> 
    public Task<object?> ProcessAsync(SchemaContext context, DataNode owner) => Task.FromResult(Value);
}

/// <summary>
/// Declare relation call field for the relation
/// </summary>
[Meta<Alias>("assign")]
[Meta<ForSchema>(SCHEMA_KIND_RELATION)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_RELATION}.assign")]
[Meta<Property.Record.RelationKind>("assign", 0)]
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(RelationSchema.Kind)}", "assign")]
[Relation<OverrideType>($"$assign.{nameof(RelationAssign.Value)}", $"{NS_SYSTEM_SCHEMA_REFLECT}.{nameof(SystemReflect.getproptype)}", $"${nameof(RelationSchema.Property)}")]
public class RelationAssignProperty : Property<RelationAssign>;

#endregion

#region Relation Call Process

/// <summary>
/// The relation call
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_RELATION}.call")]
public class RelationCall : IRelationProcess, INodeReferences, INodeError
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
[Meta<Alias>("call")]
[Meta<ForSchema>(SCHEMA_KIND_RELATION)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_RELATION}.call")]
[Meta<Property.Record.RelationKind>("call", 1)]
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(RelationSchema.Kind)}", "call")]
public class RelationCallProperty : Property<RelationCall>;

#endregion