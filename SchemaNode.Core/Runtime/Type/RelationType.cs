using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property;
using SchemaNode.Property.Core;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The relation type, they are not node types, only runtime-types controlled by the node types use them
/// </summary>
public class RelationType(RelationSchema relation, IValueTypeAccess owner) : INodeReferences, IErrorProvider
{
    /// <summary>
    /// The target of the relation
    /// </summary>
    public string Target { get; } = relation.Target;

    /// <summary>
    /// The relation owner type
    /// </summary>
    public IValueTypeAccess Owner { get; } = owner;

    /// <summary>
    /// The property type the relation applied to
    /// </summary>
    public PropertyType? Property  { get; private set; }

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
    /// The process
    /// </summary>
    public IRelationProcess? Process { get; private set; }
    
    /// <summary>
    /// Process the relation and return the property with the result
    /// </summary>
    public async Task<IProperty?> ProcessAsync(SchemaContext context, IValueAccess owner, IValueAccess? target = null)
    {
        var propType = Property?.GetCsharpType();
        if (propType == null || Activator.CreateInstance(propType) is not IProperty prop) return null;
        if (Process == null || await Process.ProcessAsync(context, owner, target) is not { } value) return null;
        prop.SetValue(value);
        return prop;
    }

    /// <inheritdoc/>
    public IEnumerable<NodeType> GetReferenceTypes()
    {
        if (Process is not INodeReferences references) yield break;
        foreach (NodeType type in references.GetReferenceTypes())
            yield return type;
    }

    /// <summary>
    /// Whether the relation is used for the given property
    /// </summary>
    public bool ForProperty<T>() where T : IProperty => Property is not null && Property.GetCsharpType() == typeof(T);

    /// <summary>
    /// Load the relation type
    /// </summary>
    public async Task LoadAsync(SchemaContext context)
    {
        Property = !string.IsNullOrWhiteSpace(relation.Property) 
            ? await context.GetNodeTypeAsync<PropertyType>(relation.Property)
            : null;
        if (Property?.GetCsharpType() == null)
        {
            Error = ErrorCodes.NO_DEFINITION;
            return;
        }
        
        foreach (Type propType in context.Runtime.GetSchemaKindProperties(SCHEMA_KIND_RELATION))
        {
            if (!Kind.Equals(propType.GetMetaProperty<Property.Record.RelationKind>()?.Value, StringComparison.OrdinalIgnoreCase)) continue;
            
            Type? processType = propType.GetMetaProperty<RelationProcess>()?.Value;
            Process = processType != null ? (IRelationProcess)Activator.CreateInstance(processType)! : null;
            if (Process == null)
            {
                Error = ErrorCodes.RELATION_PROPERTY_NOT_VALID;
                break;
            }
            await Process.LoadAsync(context, relation, Owner);
            if (Process is IErrorProvider error && !string.IsNullOrWhiteSpace(error.Error))
                Error = error.Error;
        }
    }
}

public static class RelationTypeExtensions
{
    /// <summary>
    /// Load the relation schema as relation runtime type
    /// </summary>
    public static async Task<RelationType> LoadAsync(this RelationSchema relation, SchemaContext context, IValueTypeAccess owner)
    {
        RelationType type = new(relation, owner);
        await type.LoadAsync(context);
        return type;
    }
}