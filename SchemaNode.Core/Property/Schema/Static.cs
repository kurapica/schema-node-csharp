using SchemaNode.Attribute;

namespace SchemaNode.Property.Schema;

/// <summary>
/// The property is static, can't be changed by relations
/// </summary>
[Meta<Default>(true)]
public class Static : Property<bool>;
