using SchemaNode.Schema;

namespace SchemaNode.Property.Core;

/// <summary>
/// Binding the relation process
/// </summary>
public class RelationProcess : Property<Type>
{
    public override void SetValue<TValue>(TValue value)
    {
        if (value is Type type && type.IsAssignableTo(typeof(IRelationProcess)))
            base.SetValue(type);
        else
            throw new InvalidOperationException("The relation process value not valid");
    }
}
