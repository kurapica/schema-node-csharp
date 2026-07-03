using SchemaNode.Attribute;
using SchemaNode.Property;
using SchemaNode.Property.Record;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;
using ValueSchemaKind = SchemaNode.Property.Record.ValueSchemaKind;

namespace SchemaNode.Schema;

/// <summary>
/// Schema for the "object" kind — a placeholder for any value(like JSON) whose concrete type is resolved by a Relation.
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_OBJECT, SCHEMA_KIND_ORDER_OBJECT)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_OBJECT, SCHEMA_KIND_ORDER_OBJECT)]
[Meta<ValueSchemaKind>(SCHEMA_KIND_OBJECT, SCHEMA_KIND_ORDER_OBJECT)]
[Meta<NodeType>(typeof(Runtime.ObjectType))]
public sealed class ObjectSchema;

/// <summary>
/// The scalar schema
/// </summary>
public abstract class ScalarSchema: PropertyOwner
{
    /// <summary>
    /// The base scalar schema to inherit from
    /// </summary>
    public virtual string? Base { get; set; }
}

/// <summary>
/// Represents the scalar types, the T defines the scalar type's C# type
/// </summary>
public interface IScalarType<T>;