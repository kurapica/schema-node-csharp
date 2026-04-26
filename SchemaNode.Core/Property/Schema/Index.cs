using SchemaNode.Attribute;

namespace SchemaNode.Property.Schema;

/// <summary>
/// The primary index
/// </summary>
[Meta<Default>("primary")]
public class PrimaryIndex : OrderProperty<string>;

/// <summary>
/// The unique index
/// </summary>
public class UniqueIndex: OrderProperty<string>;

/// <summary>
/// The index
/// </summary>
public class Index: OrderProperty<string>;
