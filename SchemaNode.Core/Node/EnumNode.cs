using System.Collections.Immutable;
using SchemaNode.Runtime;
using ValueType = SchemaNode.Runtime.ValueType;

namespace SchemaNode.Node;

public class EnumNode : DataNode
{
    public bool Equals(DataNode? other)
    {
        throw new NotImplementedException();
    }
    
    /// <inheritdoc/>
    public ValueType Type { get; init; }
    
    /// <inheritdoc/>
    public ImmutableArray<string>? Violated { get; set; }
    
    /// <inheritdoc/>
    public bool IsEmpty { get; }
    
    /// <inheritdoc/>
    public void SetValue<T>(T? value)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public T? GetValue<T>()
    {
        throw new NotImplementedException();
    }
}
