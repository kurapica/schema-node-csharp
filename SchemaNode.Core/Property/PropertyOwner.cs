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
        /// </summary>
        public IEnumerable<IProperty> GetProperties(IEnumerable<Type> types)
        {
            HashSet<Type> unStackableProperties = [];
            return from type in types
                from prop in owner.GetProperties(type).Where(p => p.HasValue)
                where prop.Stackable || unStackableProperties.Add(prop.GetType())
                select prop;
        }
    }
}