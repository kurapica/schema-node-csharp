namespace SchemaNode.Property;

/// <summary>
/// The interface for property owner, which can hold multiple properties.
/// </summary>
public interface IPropertyOwner
{
    /// <summary>
    /// Gets the property by type
    /// </summary>
    IProperty? GetProperty(Type type);
    
    /// <summary>
    /// Gets the properties by type, normally for stackable properties
    /// </summary>
    IEnumerable<IProperty> GetProperties(Type type);

    /// <summary>
    /// Sets the property and return itself
    /// </summary>
    void SetProperty(IProperty property);
}

public static class PropertyOwnerExtensions
{
    extension(IPropertyOwner owner)
    {
    /// <summary>
    /// Gets the property by type
    /// </summary>
    public T? GetProperty<T>() where T : class, IProperty => owner.GetProperty(typeof(T)) as T;
    
    /// <summary>
    /// Gets the property by type
    /// </summary>
    public IEnumerable<T> GetProperties<T>() where T : class, IProperty => owner.GetProperties(typeof(T)).Cast<T>();

    /// <summary>
    /// Set the property with type and return itself
    /// </summary>
    public void SetProperty<TK, TV>(TV value) where TK : Property<TV>, new()
    {
        IProperty property = Activator.CreateInstance<TK>();
        property.SetValue(value);
        owner.SetProperty(property);
    }

    /// <summary>
    /// Sets the property with the given property type and value
    /// </summary>
    public void SetProperty<T>(Type type, T value)
    {
        if (Activator.CreateInstance(type) is not IProperty prop) return;
        prop.SetValue(value);
        owner.SetProperty(prop);
    }
    
    /// <summary>
    /// Gets properties from the given types. The properties will be returned in the order of the given types.
    /// If there are duplicate properties, the properties from the later types will overwrite the previous ones.
    /// If a property has dependencies, it will only be returned when all its dependencies are satisfied.
    /// If a property has overrides, it will override the properties with the same name from the previous types.
    /// </summary>
    public List<IProperty> GetProperties(IEnumerable<Type> types)
    {
        List<IProperty> props = [];
        foreach (Type type in types)
        {
            bool first = true;
            foreach (IProperty prop in owner.GetProperties(type).Where(p => p.HasValue))
            {
                if (first)
                {
                    if (prop.Depends is { Length: > 0 } depends &&
                        // ReSharper disable once AccessToModifiedClosure
                        depends.Any(d => props.All(p => !p.Name.Equals(d, StringComparison.OrdinalIgnoreCase)))) break;
                    if (prop.Overrides is { Length: > 0 } overrides)
                        props = props.Where(p =>
                                !overrides.Any(o => o.Equals(p.Name, StringComparison.OrdinalIgnoreCase)))
                            .ToList();
                    first = false;
                }
                props.Add(prop);
            }
        }
        return props;
    }
    }
}