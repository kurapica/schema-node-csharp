using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Runtime;

namespace SchemaNode.Property;

/// <summary>
/// Mark a property as a type reference, which indicates that the property value is a reference to another type
/// </summary>
public interface ITypeRefProperty : IProperty
{
    /// <summary>
    /// Gets the reference types
    /// </summary>
    /// <returns></returns>
    public virtual IEnumerable<string> GetRefTypes()
    {
        if (GetValue<string>() is {} s)
            yield return s;
    }
}