using SchemaNode.Context;
using SchemaNode.Node;

namespace SchemaNode.Runtime;

/// <summary>
/// The generic type do nothing
/// </summary>
internal sealed class GenericType: ValueType
{
    /// <summary>
    /// The name of the generic type parameter
    /// </summary>
    public new string Name { get; set; } = null!;

    public override Task<DataNode> ValidateValueAsync(SchemaContext context, object? value)
    {
        throw new NotImplementedException();
    }

    public override DataNode ParseValue(object? value)
    {
        throw new NotImplementedException();
    }
}