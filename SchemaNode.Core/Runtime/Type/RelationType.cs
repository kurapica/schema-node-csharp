using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The relation type, they are not node types, only runtime-types controlled by the node types use them
/// </summary>
public class RelationType(RelationSchema relation, ValueType owner) : INodeReferences, INodeError
{
    /// <summary>
    /// The target of the relation
    /// </summary>
    public string Target { get; } = relation.Target;

    /// <summary>
    /// The relation owner type
    /// </summary>
    public ValueType Owner { get; } = owner;

    /// <summary>
    /// The property type the relation applied to
    /// </summary>
    public string Property { get; } = relation.Property;

    /// <summary>
    /// The stage of the relation been applied
    /// </summary>
    public RelationStage Stage { get; } = relation.Stage;

    /// <summary>
    /// The relation kind
    /// </summary>
    public string Kind { get; } = relation.Kind;

    /// <inheritdoc/>
    public string? Error { get; private set; }
    
    /// <summary>
    /// The relation process
    /// </summary>
    private IRelationProcess? _process;
    
    private PropertyType? _prop;

    /// <summary>
    /// Process the relation and return the property with the result
    /// </summary>
    public async Task<IProperty?> ProcessAsync(SchemaContext context, DataNode owner)
    {
        var propType = _prop?.GetCsharpType();
        if (propType == null || Activator.CreateInstance(propType) is not IProperty prop) return null;
        if (_process == null || await _process.ProcessAsync(context, owner) is not { } value) return null;
        prop.SetValue(value);
        return prop;
    }

    /// <inheritdoc/>
    public IEnumerable<NodeType> GetReferenceTypes()
    {
        if (_process is not INodeReferences references) yield break;
        foreach (NodeType type in references.GetReferenceTypes())
            yield return type;
    }

    /// <summary>
    /// Load the relation type
    /// </summary>
    public async Task LoadAsync(SchemaContext context)
    {
        _prop = !string.IsNullOrWhiteSpace(Property) 
            ? await context.GetNodeTypeAsync<PropertyType>(Property)
            : null;
        if (_prop?.GetCsharpType() == null)
        {
            Error = ErrorCodes.NO_DEFINITION;
            return;
        }
        
        foreach (Type propType in context.Runtime.GetSchemaKindProperties(SCHEMA_KIND_RELATION))
        {
            if (!Kind.Equals(propType.GetMetaProperty<Property.Record.RelationKind>()?.Value, StringComparison.OrdinalIgnoreCase)) continue;
            IProperty? prop = relation.GetProperty(propType);
            if (prop is not { HasValue: true } || prop.GetValue<IRelationProcess>(true) is not { } process) continue;
            _process = process;
            await _process.LoadAsync(context, Owner);
            if (_process is INodeError error && !string.IsNullOrWhiteSpace(error.Error))
                Error = error.Error;
        }
    }
}