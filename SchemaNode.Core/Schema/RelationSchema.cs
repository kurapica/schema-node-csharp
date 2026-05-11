using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Property.Presentation;
using SchemaNode.Property.Schema;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using RelationKind = SchemaNode.Enum.RelationKind;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/// <summary>
/// The relation schemas
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_RELATION}.schema")]
[Meta<SchemaKind>(SCHEMA_KIND_RELATION, SCHEMA_KIND_ORDER_RELATION)]
public class RelationSchema: ExtensibleSchema
{
    /// <summary>
    /// The target of the relation
    /// </summary>
    public string Target { get; set; } = null!;

    /// <summary>
    /// The property the relation applied to
    /// </summary>
    [Meta<SchemaType>(typeof(PropertyName))]
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
    /// <summary>
    /// The target of the relation
    /// </summary>
    string Target { get; }

    /// <summary>
    /// The property the relation applied to
    /// </summary>
    string Property { get; } 
    
    /// <summary>
    /// The stage of the relation been applied
    /// </summary>
    public RelationStage Stage { get; }

    /// <summary>
    /// Process the relation
    /// </summary>
    Task<DataNode?> ProcessAsync(SchemaContext context, DataNode owner);
}

/// <summary>
/// Build the relation process
/// </summary>
public interface IRelationProcessBuilder
{
    Task<IRelationProcess> BuildAsync(SchemaContext context, Runtime.ValueType valueType, RelationSchema relation);
}

/// <summary>
/// The relation property for data schemas
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRUCT, SCHEMA_KIND_ARRAY)]
public class Relations: Property<RelationSchema[]>;

#region Relation Call Process

/// <summary>
/// The relation call
/// </summary>
public class RelationCall : IRelationProcess, INodeReferences, INodeError
{
    /// <inheritdoc/>
    public string Target { get; init; } = string.Empty;
    
    /// <inheritdoc/>
    public string Property { get; init; } = string.Empty;
    
    /// <inheritdoc/>
    public RelationStage Stage { get; init; } = RelationStage.Load | RelationStage.Input;

    /// <summary>
    /// The call arguments
    /// </summary>
    public CallArg[] Args { get; init; } = [];
    
    /// <summary>
    /// The function type
    /// </summary>
    public FunctionType? Function { get; init; }

    /// <summary>
    /// The load error
    /// </summary>
    public string? Error { get; init; }

    public async Task<DataNode?> ProcessAsync(SchemaContext context, DataNode owner)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public IEnumerable<Runtime.NodeType> GetReferenceTypes()
    {
        if (Function is not null)
            yield return Function;
    }
}

[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_RELATION}.call")]
public class RelationCallBuidler: IRelationProcessBuilder
{
    /// <summary>
    /// The function to be used
    /// </summary>
    [Meta<SchemaType>(typeof(FuncType))]
    public string Func { get; set; } = null!;

    /// <summary>
    /// The call arguments
    /// </summary>
    public CallArg[] Args { get; set; } = [];

    /// <summary>
    /// Generate the node process
    /// </summary>
    public async Task<IRelationProcess> BuildAsync(SchemaContext context, Runtime.ValueType valueType, RelationSchema relation)
    {
        FunctionType? func = !string.IsNullOrWhiteSpace(Func) ? await context.GetNodeTypeAsync<FunctionType>(Func): null;
        string? error = func == null ? ErrorCodes.RELATION_FUNC_NOT_EXIST : null;
        
        // check args
        foreach (var arg in Args)
        {
            if (!string.IsNullOrWhiteSpace(arg.Source) && valueType.GetAccessValueType(arg.Source) == null)
                error ??= ErrorCodes.STRUCT_RELATION_WRONG_ARGS;
        }
        
        return new RelationCall
        {
            Target = relation.Target,
            Property = relation.Property,
            Stage = relation.Stage,
            Function = func,
            Args = Args,
            Error = error
        };
    }
}

/// <summary>
/// Declare relation call field for the relation
/// </summary>
[Meta<Alias>("call")]
[Meta<ForSchema>(SCHEMA_KIND_RELATION)]
[Meta<Property.Record.RelationKind>("call", 0)]
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(RelationSchema.Kind)}", "call")]
public class RelationCallProperty : Property<RelationCallBuidler>;

#endregion

/// <summary>
/// The extension method to load relation schema into relation process
/// </summary>
public static class RelationExtension
{
    /// <summary>
    /// Generate the relation process based on the relation schema
    /// </summary>
    public static async Task<IRelationProcess?> GetRelationProcessAsync(this SchemaContext context, Runtime.ValueType valueType, RelationSchema relation)
    {
        foreach (Type propType in context.Runtime.GetSchemaKindProperties(SCHEMA_KIND_RELATION))
        {
            if (relation.Kind.Equals(propType.GetMetaProperty<Property.Record.RelationKind>()?.Value, StringComparison.OrdinalIgnoreCase)) continue;
            IProperty? prop = relation.GetProperty(propType);
            if (prop is not { HasValue: true }) continue;
            if (prop.GetValue<IRelationProcess>(true) is {}  process)return process;
            if (prop.GetValue<IRelationProcessBuilder>(true) is {} processBuilder)
                return await processBuilder.BuildAsync(context, valueType, relation);
        }

        return null;
    }
}