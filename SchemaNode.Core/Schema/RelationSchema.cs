using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Property.Presentation;
using SchemaNode.Property.Schema;
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
    [Meta<SchemaType>(typeof(Scalar.Schema.Property))]
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
}

/// <summary>
/// The relation owner
/// </summary>
public interface IRelationOwner
{
    /// <summary>
    /// Gets the value by source
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    Node.DataNode? GetSourceValue(string source);

    /// <summary>
    /// Sets the target's property value
    /// </summary>
    /// <param name="target"></param>
    /// <param name="prop"></param>
    /// <param name="value"></param>
    void SetPropertyValue(string target, string prop, Node.DataNode? value);
}

/// <summary>
/// The handler to process the relation
/// </summary>
public interface IRelationProcess
{
    Task<Node.DataNode?> ProcessAsync(SchemaContext context, IRelationOwner target);
}
/// <summary>
/// The relation property for data schemas
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRUCT, SCHEMA_KIND_ARRAY)]
public class RelationsProperty: Property<RelationSchema[]>;

#region Relation Call Process

[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_RELATION}.call")]
public class RelationCall: IRelationProcess
{
    /// <summary>
    /// The function to be used
    /// </summary>
    [Meta<SchemaType>(typeof(Scalar.Schema.FuncType))]
    public string Func { get; set; } = null!;

    /// <summary>
    /// The call arguments
    /// </summary>
    public CallArg[] Args { get; set; } = [];

    /// <inheritdoc/>
    public Task<Node.DataNode?> ProcessAsync(SchemaContext context, IRelationOwner target)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Declare relation call field for the relation
/// </summary>
[Meta<Alias>("call")]
[Meta<ForSchema>(SCHEMA_KIND_RELATION)]
[Meta<Property.Record.RelationKind>("call", 0)]
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(RelationSchema.Kind)}", "call")]
public class RelationCallProperty : Property<RelationCall>;

#endregion
