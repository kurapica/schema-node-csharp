using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;

namespace SchemaNode.Attribute;

/// <summary>
/// The interface for property attributes
/// </summary>
interface IPropertyAttribute 
{
    IProperty Property { get; }
}

/// <summary>
/// Declare schema properties, like [SchemaProp(nameof(UnitProperty), "The unit of the value")], which can be used in schema extensions
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Assembly | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Method, AllowMultiple = true)]
public sealed class MetaAttribute<TP, TV>: System.Attribute, IPropertyAttribute where TP: Property<TV>, new()
{
    public MetaAttribute(TV value) => Property.SetValue(value);

    /// <summary>
    /// The property name
    /// </summary>
    public IProperty Property { get; set; } = new TP();
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Assembly | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Method, AllowMultiple = true)]
public sealed class MetaAttribute<TP> : System.Attribute, IPropertyAttribute where TP : IProperty, new()
{
    #region Constructor

    /// <summary>
    /// The default constructor, which will set the default value of the property if it exists
    /// </summary>
    public MetaAttribute()
    {
        object? defaultValue = GetType().GetMetaProperty<Default>()?.Value;
        if (defaultValue != null) Property.SetValue(defaultValue);
    }

    /// <summary>
    /// The meta attribute for order properties, which will set the order value and the default value if it exists
    /// </summary>
    /// <param name="order"></param>
    public MetaAttribute(int order)
    {
        if (Property is IOrderProperty p)
        {
            object? defaultValue = GetType().GetMetaProperty<Default>()?.Value;
            if (defaultValue != null) Property.SetValue(defaultValue);
            p.Order = order;
        }
        else
        {
            Property.SetValue(order);
        }
    }

    /// <summary>
    /// The meta attribute for order properties
    /// </summary>
    /// <param name="value"></param>
    /// <param name="order"></param>
    public MetaAttribute(object value, int order) {
        if (Property is IOrderProperty p)
        {
            p.Order = order;
            p.SetValue(value);
        }
        else
        {
            Property.SetValue(new []{value, order});
        }
    }
    
    /// <summary>
    /// The meta attribute for properties with single value
    /// </summary>
    /// <param name="value"></param>
    public MetaAttribute(object value) => Property.SetValue(value);

    /// <summary>
    /// The meta attribute for properties with array value
    /// </summary>
    /// <param name="values"></param>
    public MetaAttribute(params object[] values) => Property.SetValue(values);
    
    #endregion
    
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
    #region Utility
    
    static IEnumerable<T> FilterBy<T>(IEnumerable<IPropertyAttribute> attributes) where T : class, IProperty
    {
        foreach (var attr in attributes)
            if (attr.Property is T p) yield return p;
    }

    static IEnumerable<IProperty> ForSchema(IEnumerable<IProperty> properties, string kind)
    {
        return properties.Where(p =>
        {
            var metaProperty = p.GetType().GetMetaProperty<ForSchema>();
            return metaProperty?.Value != null && metaProperty.Value.Contains(kind, StringComparer.OrdinalIgnoreCase);
        });
    }

    /// <summary>
    /// Gets the meta attributes of the type
    /// </summary>
    static IEnumerable<IPropertyAttribute> GetMetaProperties(this Type type) => type.GetCustomAttributes(false).OfType<IPropertyAttribute>();

    /// <summary>
    /// Gets the meta attributes of the member
    /// </summary>
    static IEnumerable<IPropertyAttribute> GetMetaProperties(this System.Reflection.MemberInfo member) => member.GetCustomAttributes(false).OfType<IPropertyAttribute>();

    /// <summary>
    /// Gets the meta attributes of the parameter
    /// </summary>
    static IEnumerable<IPropertyAttribute> GetMetaProperties(this System.Reflection.ParameterInfo parameter) => parameter.GetCustomAttributes(false).OfType<IPropertyAttribute>();

    /// <summary>
    /// Gets the meta attributes of the assembly
    /// </summary>
    static IEnumerable<IPropertyAttribute> GetMetaProperties(this System.Reflection.Assembly assembly) => assembly.GetCustomAttributes(false).OfType<IPropertyAttribute>();

    /// <summary>
    /// Gets the meta attributes of the module
    /// </summary>
    static IEnumerable<IPropertyAttribute> GetMetaProperties(this System.Reflection.Module module) => module.GetCustomAttributes(false).OfType<IPropertyAttribute>();

    /// <summary>
    /// Gets the meta attributes of the event
    /// </summary>
    static IEnumerable<IPropertyAttribute> GetMetaProperties(this System.Reflection.EventInfo eventInfo) => eventInfo.GetCustomAttributes(false).OfType<IPropertyAttribute>();

    /// <summary>
    /// Gets the meta attributes of the field
    /// </summary>
    static IEnumerable<IPropertyAttribute> GetMetaProperties(this System.Reflection.FieldInfo fieldInfo) => fieldInfo.GetCustomAttributes(false).OfType<IPropertyAttribute>();

    /// <summary>
    /// Gets the meta attributes of the constructor
    /// </summary>
    static IEnumerable<IPropertyAttribute> GetMetaProperties(this System.Reflection.ConstructorInfo constructorInfo) => constructorInfo.GetCustomAttributes(false).OfType<IPropertyAttribute>();

    /// <summary>
    /// Gets the meta attributes of the method
    /// </summary>
    static IEnumerable<IPropertyAttribute> GetMetaProperties(this System.Reflection.MethodInfo methodInfo) => methodInfo.GetCustomAttributes(false).OfType<IPropertyAttribute>();

    /// <summary>
    /// Gets the meta attributes of the property
    /// </summary>
    static IEnumerable<IPropertyAttribute> GetMetaProperties(this System.Reflection.PropertyInfo propertyInfo) => propertyInfo.GetCustomAttributes(false).OfType<IPropertyAttribute>();
    
    #endregion
    
    #region Get meta properties
    
    /// <summary>
    /// Gets the meta attribute for the given property type from the type
    /// </summary>
    public static IEnumerable<T> GetMetaProperties<T>(this Type type) where T : class, IProperty => FilterBy<T>(type.GetMetaProperties());

    /// <summary>
    /// Gets the meta attribute for the given property type from the member
    /// </summary>
    public static IEnumerable<T> GetMetaProperties<T>(this System.Reflection.MemberInfo member) where T : class, IProperty => FilterBy<T>(member.GetMetaProperties());

    /// <summary>
    /// Gets the meta attribute for the given property type from the parameter
    /// </summary>
    public static IEnumerable<T> GetMetaProperties<T>(this System.Reflection.ParameterInfo parameter) where T : class, IProperty => FilterBy<T>(parameter.GetMetaProperties());

    /// <summary>
    /// Gets the meta attribute for the given property type from the assembly
    /// </summary>
    public static IEnumerable<T> GetMetaProperties<T>(this System.Reflection.Assembly assembly) where T : class, IProperty => FilterBy<T>(assembly.GetMetaProperties());

    /// <summary>
    /// Gets the meta attribute for the given property type from the module
    /// </summary>
    public static IEnumerable<T> GetMetaProperties<T>(this System.Reflection.Module module) where T : class, IProperty => FilterBy<T>(module.GetMetaProperties());

    /// <summary>
    /// Gets the meta attribute for the given property type from the event
    /// </summary>
    public static IEnumerable<T> GetMetaProperties<T>(this System.Reflection.EventInfo eventInfo) where T : class, IProperty => FilterBy<T>(eventInfo.GetMetaProperties());

    /// <summary>
    /// Gets the meta attribute for the given property type from the field
    /// </summary>
    public static IEnumerable<T> GetMetaProperties<T>(this System.Reflection.FieldInfo fieldInfo) where T : class, IProperty => FilterBy<T>(fieldInfo.GetMetaProperties());

    /// <summary>
    /// Gets the meta attribute for the given property type from the constructor
    /// </summary>
    public static IEnumerable<T> GetMetaProperties<T>(this System.Reflection.ConstructorInfo constructorInfo) where T : class, IProperty => FilterBy<T>(constructorInfo.GetMetaProperties());

    /// <summary>
    /// Gets the meta attribute for the given property type from the method
    /// </summary>
    public static IEnumerable<T> GetMetaProperties<T>(this System.Reflection.MethodInfo methodInfo) where T : class, IProperty => FilterBy<T>(methodInfo.GetMetaProperties());

    /// <summary>
    /// Gets the meta attribute for the given property type from the property
    /// </summary>
    public static IEnumerable<T> GetMetaProperties<T>(this System.Reflection.PropertyInfo propertyInfo) where T : class, IProperty => FilterBy<T>(propertyInfo.GetMetaProperties());
    
    #endregion
    
    #region Get meta properties for schema kind
    
    /// <summary>
    /// Gets the meta attribute for the given property type from the type
    /// </summary>
    public static IEnumerable<IProperty> GetMetaPropertiesForSchema<T>(this Type type, string kind) where T : class, IProperty => ForSchema(FilterBy<T>(type.GetMetaProperties()), kind);

    /// <summary>
    /// Gets the meta attribute for the given property type from the member
    /// </summary>
    public static IEnumerable<IProperty> GetMetaPropertiesForSchema<T>(this System.Reflection.MemberInfo member, string kind) where T : class, IProperty => ForSchema(FilterBy<T>(member.GetMetaProperties()), kind);

    /// <summary>
    /// Gets the meta attribute for the given property type from the parameter
    /// </summary>
    public static IEnumerable<IProperty> GetMetaPropertiesForSchema<T>(this System.Reflection.ParameterInfo parameter, string kind) where T : class, IProperty => ForSchema(FilterBy<T>(parameter.GetMetaProperties()), kind);

    /// <summary>
    /// Gets the meta attribute for the given property type from the assembly
    /// </summary>
    public static IEnumerable<IProperty> GetMetaPropertiesForSchema<T>(this System.Reflection.Assembly assembly, string kind) where T : class, IProperty => ForSchema(FilterBy<T>(assembly.GetMetaProperties()), kind);

    /// <summary>
    /// Gets the meta attribute for the given property type from the module
    /// </summary>
    public static IEnumerable<IProperty> GetMetaPropertiesForSchema<T>(this System.Reflection.Module module, string kind) where T : class, IProperty => ForSchema(FilterBy<T>(module.GetMetaProperties()), kind);

    /// <summary>
    /// Gets the meta attribute for the given property type from the event
    /// </summary>
    public static IEnumerable<IProperty> GetMetaPropertiesForSchema<T>(this System.Reflection.EventInfo eventInfo, string kind) where T : class, IProperty => ForSchema(FilterBy<T>(eventInfo.GetMetaProperties()), kind);

    /// <summary>
    /// Gets the meta attribute for the given property type from the field
    /// </summary>
    public static IEnumerable<IProperty> GetMetaPropertiesForSchema<T>(this System.Reflection.FieldInfo fieldInfo, string kind) where T : class, IProperty => ForSchema(FilterBy<T>(fieldInfo.GetMetaProperties()), kind);

    /// <summary>
    /// Gets the meta attribute for the given property type from the constructor
    /// </summary>
    public static IEnumerable<IProperty> GetMetaPropertiesForSchema<T>(this System.Reflection.ConstructorInfo constructorInfo, string kind) where T : class, IProperty => ForSchema(FilterBy<T>(constructorInfo.GetMetaProperties()), kind);

    /// <summary>
    /// Gets the meta attribute for the given property type from the method
    /// </summary>
    public static IEnumerable<IProperty> GetMetaPropertiesForSchema<T>(this System.Reflection.MethodInfo methodInfo, string kind) where T : class, IProperty => ForSchema(FilterBy<T>(methodInfo.GetMetaProperties()), kind);

    /// <summary>
    /// Gets the meta attribute for the given property type from the property
    /// </summary>
    public static IEnumerable<IProperty> GetMetaPropertiesForSchema<T>(this System.Reflection.PropertyInfo propertyInfo, string kind) where T : class, IProperty => ForSchema(FilterBy<T>(propertyInfo.GetMetaProperties()), kind);
    
    #endregion
    
    #region Get meta property
    
    /// <summary>
    /// Gets the meta attribute for the given property type from the type
    /// </summary>
    public static T? GetMetaProperty<T>(this Type type) where T : class, IProperty => FilterBy<T>(type.GetMetaProperties()).FirstOrDefault();

    /// <summary>
    /// Gets the meta attribute for the given property type from the member
    /// </summary>
    public static T? GetMetaProperty<T>(this System.Reflection.MemberInfo member) where T : class, IProperty => FilterBy<T>(member.GetMetaProperties()).FirstOrDefault();

    /// <summary>
    /// Gets the meta attribute for the given property type from the parameter
    /// </summary>
    public static T? GetMetaProperty<T>(this System.Reflection.ParameterInfo parameter) where T : class, IProperty => FilterBy<T>(parameter.GetMetaProperties()).FirstOrDefault();

    /// <summary>
    /// Gets the meta attribute for the given property type from the assembly
    /// </summary>
    public static T? GetMetaProperty<T>(this System.Reflection.Assembly assembly) where T : class, IProperty => FilterBy<T>(assembly.GetMetaProperties()).FirstOrDefault();

    /// <summary>
    /// Gets the meta attribute for the given property type from the module
    /// </summary>
    public static T? GetMetaProperty<T>(this System.Reflection.Module module) where T : class, IProperty => FilterBy<T>(module.GetMetaProperties()).FirstOrDefault();

    /// <summary>
    /// Gets the meta attribute for the given property type from the event
    /// </summary>
    public static T? GetMetaProperty<T>(this System.Reflection.EventInfo eventInfo) where T : class, IProperty => FilterBy<T>(eventInfo.GetMetaProperties()).FirstOrDefault();

    /// <summary>
    /// Gets the meta attribute for the given property type from the field
    /// </summary>
    public static T? GetMetaProperty<T>(this System.Reflection.FieldInfo fieldInfo) where T : class, IProperty => FilterBy<T>(fieldInfo.GetMetaProperties()).FirstOrDefault();

    /// <summary>
    /// Gets the meta attribute for the given property type from the constructor
    /// </summary>
    public static T? GetMetaProperty<T>(this System.Reflection.ConstructorInfo constructorInfo) where T : class, IProperty => FilterBy<T>(constructorInfo.GetMetaProperties()).FirstOrDefault();

    /// <summary>
    /// Gets the meta attribute for the given property type from the method
    /// </summary>
    public static T? GetMetaProperty<T>(this System.Reflection.MethodInfo methodInfo) where T : class, IProperty => FilterBy<T>(methodInfo.GetMetaProperties()).FirstOrDefault();

    /// <summary>
    /// Gets the meta attribute for the given property type from the property
    /// </summary>
    public static T? GetMetaProperty<T>(this System.Reflection.PropertyInfo propertyInfo) where T : class, IProperty => FilterBy<T>(propertyInfo.GetMetaProperties()).FirstOrDefault();
    
    #endregion
}