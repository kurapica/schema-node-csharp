namespace SchemaNode.Attribute;


/// <summary>
/// The default argument so we can declare not-nullable function for nullable arguments
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false, AllowMultiple = false)]
public class DefaultAttribute(object value) : System.Attribute
{
    public object Value { get; } = value;
}
