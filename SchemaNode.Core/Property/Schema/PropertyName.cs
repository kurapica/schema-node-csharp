namespace SchemaNode.Property.Schema;

/// <summary>
/// The property name which is used to specify the property in the schema, if omit, the system will use the property type name (without "Property" suffix) as the property name by default.
/// </summary>
public sealed class PropertyName : Property<string>;
