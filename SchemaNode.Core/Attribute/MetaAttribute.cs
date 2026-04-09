using SchemaNode.Property;

namespace SchemaNode.Attribute;

/// <summary>
/// The interface for property attributes
/// </summary>
public interface IProperyAttribute {
    IProperty Property { get; }
}

/// <summary>
/// Declare schema properties, like [SchemaProp(nameof(UnitProperty), "The unit of the value")], which can be used in schema extensions
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Assembly | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Method, AllowMultiple = true)]
public class MetaAttribute<TP, TV>: System.Attribute, IProperyAttribute where TP: Property<TV>, new()
{
    public MetaAttribute(TV value) => Property.SetValue(value);

    /// <summary>
    /// The property name
    /// </summary>
    public IProperty Property { get; set; } = new TP();
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Assembly | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Method, AllowMultiple = true)]
public class MetaAttribute<TP> : System.Attribute, IProperyAttribute where TP : IProperty, new()
{
    public MetaAttribute(bool value) => Property.SetValue(value);
    public MetaAttribute(byte value) => Property.SetValue(value);
    public MetaAttribute(sbyte value) => Property.SetValue(value);
    public MetaAttribute(short value) => Property.SetValue(value);
    public MetaAttribute(ushort value) => Property.SetValue(value);
    public MetaAttribute(int value) => Property.SetValue(value);
    public MetaAttribute(uint value) => Property.SetValue(value);
    public MetaAttribute(long value) => Property.SetValue(value);
    public MetaAttribute(ulong value) => Property.SetValue(value);
    public MetaAttribute(float value) => Property.SetValue(value);
    public MetaAttribute(double value) => Property.SetValue(value);
    public MetaAttribute(char value) => Property.SetValue(value);
    public MetaAttribute(string value) => Property.SetValue(value);
    public MetaAttribute(params bool[] value) => Property.SetValue(value);
    public MetaAttribute(params byte[] value) => Property.SetValue(value);
    public MetaAttribute(params sbyte[] value) => Property.SetValue(value);
    public MetaAttribute(params short[] value) => Property.SetValue(value);
    public MetaAttribute(params ushort[] value) => Property.SetValue(value);
    public MetaAttribute(params int[] value) => Property.SetValue(value);
    public MetaAttribute(params uint[] value) => Property.SetValue(value);
    public MetaAttribute(params long[] value) => Property.SetValue(value);
    public MetaAttribute(params ulong[] value) => Property.SetValue(value);
    public MetaAttribute(params float[] value) => Property.SetValue(value);
    public MetaAttribute(params double[] value) => Property.SetValue(value);
    public MetaAttribute(params char[] value) => Property.SetValue(value);
    public MetaAttribute(params string[] value) => Property.SetValue(value);

    /// <summary>
    /// The property name
    /// </summary>
    public IProperty Property { get; set; } = new TP();
}

/// <summary>
/// The extension to get meta properties from the reflection objects
/// </summary>
public static class MetaExtension
{
    static T? GetMetaAttribute<T>(IEnumerable<object> attributes) where T : class, IProperty
    {
        var targetType = typeof(T);

        foreach (var attr in attributes)
        {
            var at = attr.GetType();
            if (!at.IsGenericType) continue;

            var def = at.GetGenericTypeDefinition();

            if (def == typeof(MetaAttribute<>))
            {
                var tp = at.GetGenericArguments()[0];
                if (tp == targetType)
                    return ((IProperyAttribute)attr).Property as T;
            }
            else if (def == typeof(MetaAttribute<,>))
            {
                var tp = at.GetGenericArguments()[0];
                if (tp == targetType)
                    return ((IProperyAttribute)attr).Property as T;
            }
        }

        return default(T?);
    }


    /// <summary>
    /// Gets the meta attribtues of the type
    /// </summary>
    public static IEnumerable<IProperyAttribute> GetMetaAttributes(this Type type) => type.GetCustomAttributes(false).OfType<IProperyAttribute>();

    /// <summary>
    /// Gets the meta attribtues of the member
    /// </summary>
    public static IEnumerable<IProperyAttribute> GetMetaAttributes(this System.Reflection.MemberInfo member) => member.GetCustomAttributes(false).OfType<IProperyAttribute>();

    /// <summary>
    /// Gets the meta attribtues of the parameter
    /// </summary>
    public static IEnumerable<IProperyAttribute> GetMetaAttributes(this System.Reflection.ParameterInfo parameter) => parameter.GetCustomAttributes(false).OfType<IProperyAttribute>();

    /// <summary>
    /// Gets the meta attribtues of the assembly
    /// </summary>
    public static IEnumerable<IProperyAttribute> GetMetaAttributes(this System.Reflection.Assembly assembly) => assembly.GetCustomAttributes(false).OfType<IProperyAttribute>();

    /// <summary>
    /// Gets the meta attribtues of the module
    /// </summary>
    public static IEnumerable<IProperyAttribute> GetMetaAttributes(this System.Reflection.Module module) => module.GetCustomAttributes(false).OfType<IProperyAttribute>();

    /// <summary>
    /// Gets the meta attribtues of the event
    /// </summary>
    public static IEnumerable<IProperyAttribute> GetMetaAttributes(this System.Reflection.EventInfo eventInfo) => eventInfo.GetCustomAttributes(false).OfType<IProperyAttribute>();

    /// <summary>
    /// Gets the meta attribtues of the field
    /// </summary>
    public static IEnumerable<IProperyAttribute> GetMetaAttributes(this System.Reflection.FieldInfo fieldInfo) => fieldInfo.GetCustomAttributes(false).OfType<IProperyAttribute>();

    /// <summary>
    /// Gets the meta attribtues of the constructor
    /// </summary>
    public static IEnumerable<IProperyAttribute> GetMetaAttributes(this System.Reflection.ConstructorInfo constructorInfo) => constructorInfo.GetCustomAttributes(false).OfType<IProperyAttribute>();

    /// <summary>
    /// Gets the meta attribtues of the method
    /// </summary>
    public static IEnumerable<IProperyAttribute> GetMetaAttributes(this System.Reflection.MethodInfo methodInfo) => methodInfo.GetCustomAttributes(false).OfType<IProperyAttribute>();

    /// <summary>
    /// Gets the meta attribtues of the property
    /// </summary>
    public static IEnumerable<IProperyAttribute> GetMetaAttributes(this System.Reflection.PropertyInfo propertyInfo) => propertyInfo.GetCustomAttributes(false).OfType<IProperyAttribute>();
    
    /// <summary>
    /// Gets the meta attribute for the given property type from the type
    /// </summary>
    public static T? GetMetaAttribute<T>(this Type type) where T : class, IProperty => GetMetaAttribute<T>(type.GetMetaAttributes());

    /// <summary>
    /// Gets the meta attribute for the given property type from the member
    /// </summary>
    public static T? GetMetaAttribute<T>(this System.Reflection.MemberInfo member) where T : class, IProperty => GetMetaAttribute<T>(member.GetMetaAttributes());

    /// <summary>
    /// Gets the meta attribute for the given property type from the parameter
    /// </summary>
    public static T? GetMetaAttribute<T>(this System.Reflection.ParameterInfo parameter) where T : class, IProperty => GetMetaAttribute<T>(parameter.GetMetaAttributes());

    /// <summary>
    /// Gets the meta attribute for the given property type from the assembly
    /// </summary>
    public static T? GetMetaAttribute<T>(this System.Reflection.Assembly assembly) where T : class, IProperty => GetMetaAttribute<T>(assembly.GetMetaAttributes());

    /// <summary>
    /// Gets the meta attribute for the given property type from the module
    /// </summary>
    public static T? GetMetaAttribute<T>(this System.Reflection.Module module) where T : class, IProperty => GetMetaAttribute<T>(module.GetMetaAttributes());

    /// <summary>
    /// Gets the meta attribute for the given property type from the event
    /// </summary>
    public static T? GetMetaAttribute<T>(this System.Reflection.EventInfo eventInfo) where T : class, IProperty => GetMetaAttribute<T>(eventInfo.GetMetaAttributes());

    /// <summary>
    /// Gets the meta attribute for the given property type from the field
    /// </summary>
    public static T? GetMetaAttribute<T>(this System.Reflection.FieldInfo fieldInfo) where T : class, IProperty => GetMetaAttribute<T>(fieldInfo.GetMetaAttributes());

    /// <summary>
    /// Gets the meta attribute for the given property type from the constructor
    /// </summary>
    public static T? GetMetaAttribute<T>(this System.Reflection.ConstructorInfo constructorInfo) where T : class, IProperty => GetMetaAttribute<T>(constructorInfo.GetMetaAttributes());

    /// <summary>
    /// Gets the meta attribute for the given property type from the method
    /// </summary>
    public static T? GetMetaAttribute<T>(this System.Reflection.MethodInfo methodInfo) where T : class, IProperty => GetMetaAttribute<T>(methodInfo.GetMetaAttributes());

    /// <summary>
    /// Gets the meta attribute for the given property type from the property
    /// </summary>
    public static T? GetMetaAttribute<T>(this System.Reflection.PropertyInfo propertyInfo) where T : class, IProperty => GetMetaAttribute<T>(propertyInfo.GetMetaAttributes());
}