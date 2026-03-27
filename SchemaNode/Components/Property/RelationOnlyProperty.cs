using SchemaNode.Attribute;
using SchemaNode.Enum;

namespace SchemaNode.Components.Property;

/// <summary>
/// Declare a relation-only property for property schema
/// </summary>
[SchemaProperty([SchemaType.Property])]
public sealed class RelationOnlyProperty;

/// <summary>
/// The realtion-only property, not for settings
/// </summary>
[SchemaPropertyKind(nameof(RelationOnlyProperty))]
public interface IRelationOnlyProperty : IProperty;