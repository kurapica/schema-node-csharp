using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property;
using SchemaNode.Property.Constraint;
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
    /// The relation only works for the given schema kind(for property relation only)
    /// </summary>
    [Meta<SchemaType>(typeof(Enum.SchemaKind))]
    public string? ForSchema { get; set; }

    /// <summary>
    /// Equals check
    /// </summary>
    public bool Equals(PropertyOwner? other)
    {
        if (other is not RelationSchema otherRelation) return false;
        if (ReferenceEquals(this, otherRelation)) return true;
        return Target.Equals(otherRelation.Target, StringComparison.OrdinalIgnoreCase) && 
               Property.Equals(otherRelation.Property, StringComparison.OrdinalIgnoreCase) && 
               Stage == otherRelation.Stage && 
               Kind.Equals(otherRelation.Kind, StringComparison.OrdinalIgnoreCase) &&
               ForSchema == otherRelation.ForSchema;
    }
}

/// <summary>
/// The relation property for data schemas
/// </summary>
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.relations")]
//[Relation<EntrySource, Relation.Call>($"{nameof(Relations)}.{nameof(RelationSchema.Target)}", NS_SYSTEM_SCHEMA_REFLECT_GET_ACCESS_ENTRIES, NODE_SELF, $"@{nameof(Relations)}.{nameof(RelationSchema.Target)}")]
public class Relations : Property<RelationSchema[]>
{
    /// <inheritdoc/>
    public override bool Combine(IProperty other, ISchemaRuntime? runtime = null)
    {
        if (other is not Relations { Value.Length: > 0 } otherRelations) return false;
        if (Value is not { Length: > 0 })
        {
            SetValue(otherRelations.Value[..]);
            return true;
        }
        List<RelationSchema> combine = new (Value ?? []);
        combine.AddRange(otherRelations.Value.Where(r => !combine.Any(c => c.Equals(r))));
        SetValue(combine.ToArray());
        return true;
    }
}

/// <summary>
/// Represents the relation type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_RELATION}.type")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, NODE_SELF, SCHEMA_KIND_RELATION)]
public class RelationType: AnyType;

/// <summary>
/// The handler to process the relation, Check <see cref="RelationType"/> for details
/// </summary>
public interface IRelationProcess
{
    Task LoadAsync(SchemaContext context, RelationSchema schema, IValueTypeAccess owner);
    
    /// <summary>
    /// Process the relation and return the new property value
    /// </summary>
    Task<object?> ProcessAsync(SchemaContext context, IValueAccess owner, IValueAccess? target = null);
}