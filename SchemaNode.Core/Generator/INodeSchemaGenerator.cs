using SchemaNode.Runtime;
using SchemaNode.Schema;

namespace SchemaNode.Generator;

/// <summary>
/// The schema generator used to convert C# features into node schemas
/// </summary>
public interface INodeSchemaGenerator
{
    /// <summary>
    /// Generate the node schemas from type
    /// </summary>
    NodeSchema[]? GenerateSchema(Type type, string @namespace, Func<Type, string, string?> typeResolver);
}