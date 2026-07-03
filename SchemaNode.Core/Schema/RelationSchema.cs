using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
using RelationKind = SchemaNode.Enum.RelationKind;
using SchemaKind =  SchemaNode.Property.Record.SchemaKind;
using SchemaPropertyType = SchemaNode.Schema.PropertyType;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/// <summary>
/// The relation schemas
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_RELATION, SCHEMA_KIND_ORDER_RELATION)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_RELATION}.schema")]
[Meta<Attach>(SCHEMA_KIND_RELATION)]
public class RelationSchema : PropertyOwner
{
    /// <summary>
    /// The target of the relation
    /// </summary>
    public string Target { get; set; } = null!;

    /// <summary>
    /// The property the relation applied to
    /// </summary>
    [Meta<SchemaType>(typeof(SchemaPropertyType))]
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
    public override bool Equals(PropertyOwner? other)
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
/// The relation property for data schemas
/// </summary>
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.relations")]
[Relation<EntrySource>($"${nameof(Relations)}.{nameof(RelationSchema.Target)}", NS_SYSTEM_SCHEMA_REFLECT_GET_SUB_ENTRIES, RELATION_OWNER, NODE_SELF)]
public class Relations: Property<RelationSchema[]>;

/// <summary>
/// The handler to process the relation, Check <see cref="RelationType"/> for details
/// </summary>
public interface IRelationProcess
{
    Task LoadAsync(SchemaContext context, IValueTypeAccess owner);
    
    /// <summary>
    /// Process the relation and return the new property value
    /// </summary>
    Task<object?> ProcessAsync(SchemaContext context, IValueAccess owner);
}