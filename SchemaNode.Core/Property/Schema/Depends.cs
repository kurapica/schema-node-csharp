using SchemaNode.Attribute;
using SchemaNode.Schema;

namespace SchemaNode.Property.Schema;

/// <summary>
/// Declare depends of the property type
/// </summary>
public class Depends: Property<Type[]>;

/// <summary>
/// Decare optional depends of the property type
/// </summary>
public class OptionDepends: Property<Type[]>;