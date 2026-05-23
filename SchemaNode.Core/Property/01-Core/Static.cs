using SchemaNode.Attribute;
using SchemaNode.Property.Common;

namespace SchemaNode.Property.Core;

/// <summary>
/// The property is static, can't be changed by relations
/// </summary>
[Meta<Default>(true)]
public class Static : Property<bool>;
