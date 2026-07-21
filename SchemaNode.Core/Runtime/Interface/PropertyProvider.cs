using SchemaNode.Property;

namespace SchemaNode.Runtime;

/// <summary>
/// The property provider for property sequence access
/// </summary>
public interface IPropertyProvider
{
    /// <summary>
    /// Gets the property
    /// </summary>
    T? GetProperty<T>() where T : class, IProperty;

    /// <summary>
    /// Gets the properties
    /// </summary>
    IEnumerable<T> GetProperties<T>() where T : IProperty;
}

public static class PropertyProviderExtension
{
    /// <summary>
    /// Gets the properties from several providers
    /// </summary>
    public static IEnumerable<T> JoinProperties<T>(this IPropertyProvider provider, params IEnumerable<T>?[] providers) where T : IProperty
    {
        HashSet<Type> types = [];
        foreach (IEnumerable<T> enumerable in providers.Where(p => p is not null).Select(p => p!))
        {
            foreach (T prop in enumerable)
            {
                if (prop.Stackable) yield return prop;

                Type propType = prop.GetType();
                if (types.Add(propType))
                {
                    yield return prop;
                    if (propType == typeof(T))
                        yield break;
                }
            }
        }
    }
}