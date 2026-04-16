using SchemaNode.Attribute;

namespace SchemaNode.Property.Schema;

/// <summary>
/// The index
/// </summary>
public class Index: OrderProperty<string>;

/// <summary>
/// The unique index
/// </summary>
[Meta<Default>("main")]
public class UniqueIndex: OrderProperty<string>;