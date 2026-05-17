using System.Collections.Immutable;
using SchemaNode.Runtime;
using ValueType = SchemaNode.Runtime.ValueType;

namespace SchemaNode.Node;

public class EnumNode : DataNode
{
    public override bool IsEmpty { get; }
    public override bool TrySetValue<T>(T? value) where T : default
    {
        throw new NotImplementedException();
    }

    public override bool TryGetValue<T>(out T? value) where T : default
    {
        throw new NotImplementedException();
    }
}