using SchemaNode.Attribute;
using SchemaNode.Enum;

namespace SchemaNode.Components.Property.Presentation;

/// <summary>
/// Unpack/pack additional data for the json node.
/// </summary>
[SchemaProperty([SchemaType.StructField], [ValueSchemaType.Json])]
public class UnpackProperty : SchemaProperty<bool>;
