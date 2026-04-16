namespace SchemaNode.Property.Schema;

/// <summary>
/// The property alias name which is used to specify the property in the schema,
/// if omitted, the system will use the property type name (without "Property" suffix) as the property name by default.
/// </summary>
public sealed class Alias : Property<string>;