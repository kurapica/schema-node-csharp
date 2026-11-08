using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Property.Core;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The relation type, they are not node types, only runtime-types controlled by the node types use them
/// </summary>
public class RelationType(RelationSchema relation, IValueTypeAccess owner) : INodeReferences, IErrorProvider, IRelation
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
    public async Task<IProperty?> CallAsync(SchemaContext context, IValueAccess owner, IValueAccess? target = null)
    {
        var propType = Property?.GetCsharpType();
        if (propType == null || Activator.CreateInstance(propType) is not IProperty prop) return null;
        if (Process == null || await Process.ProcessAsync(context, owner, target ?? owner).ConfigureAwait(false) is not { } value) return null;
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
    public async Task LoadAsync(SchemaContext context, string?[] kinds, CancellationToken token = default)
    {
        Property = !string.IsNullOrWhiteSpace(relation.Property) 
            ? await context.GetPropertyType(relation.Property, token, kinds).ConfigureAwait(false)
            : null;
        if (Property?.GetCsharpType() == null)
        {
            Error = ErrorCodes.NO_DEFINITION;
            return;
        }
        
        foreach (Type propType in context.Runtime.GetSchemaKindPropertyTypes(SCHEMA_KIND_NODE_RELATION))
        {
            if (!Kind.Equals(propType.GetMetaProperty<Property.Record.RelationKind>()?.Value, StringComparison.OrdinalIgnoreCase)) continue;
            
            Type? processType = propType.GetMetaProperty<RelationProcess>()?.Value;
            Process = processType != null ? (IRelationProcess)Activator.CreateInstance(processType)! : null;
            if (Process == null)
            {
                Error = ErrorCodes.RELATION_PROPERTY_NOT_VALID;
                break;
            }
            await Process.LoadAsync(context, relation, Owner, kinds, token).ConfigureAwait(false);
            if (Process is IErrorProvider error && !string.IsNullOrWhiteSpace(error.Error))
                Error = error.Error;
        }
    }

    #region IRelation

    /// <inheritdoc/>
    Type? IRelation.PropertyCtor => Property?.GetCsharpType();

    /// <inheritdoc/>
    bool IRelation.HasDepends => Process?.hasDepends ?? false;

    /// <inheritdoc/>
    void IRelation.Attach(IValueAccess owner, IValueAccess target)
    {
        if (Property?.GetCsharpType() is not { } propType) return;
        Process?.Detach(this, owner, target); // clear first
        Process?.Attach(this, owner, target);
        if (Activator.CreateInstance(propType) is IProperty prop && !prop.InitWithRelation(this, owner, target))
            return;
        _ = ((IRelation)this).ProcessAsync(owner, target);
    }

    /// <inheritdoc/>
    void IRelation.Detach(IValueAccess owner, IValueAccess target)
    {
        if (Property?.GetCsharpType() is not { } propType) return;
        Process?.Detach(this, owner, target);
        target.SetPropertyValue(propType, null, owner, this);
    }

    /// <inheritdoc/>
    async Task IRelation.ProcessAsync(IValueAccess owner, IValueAccess target)
    {
        if (Property?.GetCsharpType() is not { } propType) return;
        if (owner is not DataNode node || node.Context is not SchemaContext context) return;

        IProperty? prop = await CallAsync(context, owner, target).ConfigureAwait(false);
        if (prop is null) return;

        target.SetPropertyValue(propType, prop.GetValue<object>(), owner, this);
    }

    #endregion
}

public static class RelationTypeExtensions
{
    /// <summary>
    /// Load the relation schema as relation runtime type
    /// </summary>
    public static async Task<RelationType> LoadAsync(this RelationSchema relation, SchemaContext context, IValueTypeAccess owner, string?[] kinds, CancellationToken token = default)
    {
        RelationType type = new(relation, owner);
        await type.LoadAsync(context, kinds, token).ConfigureAwait(false);
        return type;
    }
}