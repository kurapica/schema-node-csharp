using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.String;
using SchemaNode.Property.Struct;
using SchemaNode.Relation;
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
[Meta<SchemaKind>(SCHEMA_KIND_NODE_RELATION, SCHEMA_KIND_ORDER_RELATION)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_RELATION}.schema")]
[Meta<Attach>(SCHEMA_KIND_NODE_RELATION)]
public class RelationSchema : PropertyOwner
{
    /// <summary>
    /// The target of the relation
    /// </summary>
    [Meta<PrimaryIndex>(0)]
    [Meta<EntrySourceConsumer>(true)]
    public string Target { get; set; } = null!;

    /// <summary>
    /// The target value type
    /// </summary>
    [Meta<DisplayOnly>(true)]
    [Meta<AccessValueTypeResolver>(nameof(Target))]
    public string? TargetType { get; set; }

    /// <summary>
    /// The target kind
    /// </summary>
    [Meta<DisplayOnly>(true)]
    [Relation<Default, Call>(nameof(TargetKind), $"{NS_SYSTEM_SCHEMA_REFLECT_TYPE}.{nameof(Function.Reflect.Type.getschemakind)}", $"@{nameof(TargetType)}")]
    public string? TargetKind { get; set; }

    /// <summary>
    /// The relation owner kind
    /// </summary>
    [Meta<DisplayOnly>(true)]
    [Meta<KindResolver>(true)]
    public string? OwnerKind { get; set; }

    /// <summary>
    /// The property the relation applied to
    /// </summary>
    [Meta<PrimaryIndex>(1)]
    [Relation<EntrySource, Assign>(nameof(Property), $"{NS_SYSTEM_SCHEMA_REFLECT_PROPERTY}.{nameof(Function.Reflect.Property.getkindproperties)}", $"@{nameof(TargetKind)}", $"@{nameof(OwnerKind)}")]
    public string Property { get; set; } = null!;
    
    /// <summary>
    /// The property value type
    /// </summary>
    [Meta<DisplayOnly>(true)]
    [Relation<Default, Call>(NODE_SELF, $"{NS_SYSTEM_SCHEMA_REFLECT_PROPERTY}.{nameof(Function.Reflect.Property.getkindpropvaluetype)}", $"@{nameof(Property)}", $"@{nameof(TargetKind)}", $"@{nameof(OwnerKind)}")]
    public string? ValueType { get; set; }

    /// <summary>
    /// The relation kind
    /// </summary>
    [Meta<SchemaType>(typeof(RelationKind))]
    public string Kind { get; set; } = null!;
    
    /// <summary>
    /// Equals check
    /// </summary>
    public bool Equals(PropertyOwner? other)
    {
        if (other is not RelationSchema otherRelation) return false;
        if (ReferenceEquals(this, otherRelation)) return true;
        return Target.Equals(otherRelation.Target, StringComparison.OrdinalIgnoreCase) &&
               Property.Equals(otherRelation.Property, StringComparison.OrdinalIgnoreCase) &&
               Kind.Equals(otherRelation.Kind, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// The relation property for data schemas
/// </summary>
[Meta<OfNodeKind>(NODE_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_CORE}.relations")]
//[Relation<EntrySource, Relation.Call>($"{nameof(Relations)}.{nameof(RelationSchema.Target)}", NS_SYSTEM_SCHEMA_REFLECT_GET_ACCESS_ENTRIES, NODE_SELF, $"@{nameof(Relations)}.{nameof(RelationSchema.Target)}")]
public class Relations : Property<RelationSchema[]>
{
    public override void SetValue<TValue>(TValue value)
    {
        if (value is IEnumerable<RelationSchema> schemas)
            base.SetValue(AnyProcess.CombineAnyRelations(schemas));
        else
            base.SetValue(value);
    }

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
/// The handler to process the relation, Check <see cref="RelationType"/> for details
/// </summary>
public interface IRelationProcess
{
    /// <summary>
    /// Loading the relation schema and prepare for processing
    /// </summary>
    Task LoadAsync(SchemaContext context, RelationSchema schema, IValueTypeAccess owner, params string?[] kinds);
    
    /// <summary>
    /// Process the relation and return the new property value
    /// </summary>
    Task<object?> ProcessAsync(SchemaContext context, IValueAccess owner, IValueAccess? target = null);
}