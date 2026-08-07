using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Property.Record;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace SchemaNode.Property;

/// <summary>
/// The base class for all property owners, which can hold properties and extensions. It provides methods to get and set properties, as well as to combine extensions from other property owners.
/// </summary>
public abstract class PropertyOwner
{
    #region Extensions

    /// <summary>
    /// The dictionary to hold the extension properties. The key is the property name, and the value is the property value.
    /// </summary>
    [SchemaIgnore]
    [JsonExtensionData]
    public JsonObject? Extensions { get; internal set; }

    /// <summary>
    /// Gets the schema kind if existed
    /// </summary>
    [SchemaIgnore]
    [JsonIgnore]
    public string? SchemaKind => GetType().GetMetaProperty<SchemaKind>()?.GetValue<string>() ??
                                 GetType().GetMetaProperty<Attach>()?.GetValue<string>();

    #endregion

    #region Property Access

    /// <summary>
    /// Sets the property with the given property instance and returns itself. If the property is stackable and already exists, it will add the new value to the existing array of values. Otherwise, it will overwrite the existing value.
    /// </summary>
    public PropertyOwner SetProperty(IProperty property)
    {
        Extensions ??= [];
        var node = property.GetValue<JsonNode>();
        if (node == null) return this;

        // Keep stackable properties as array, easily for rendering
        if (property.Stackable && Extensions.TryGetValue(property.Name, out var existNode) && !existNode.IsEmpty())
        {
            if (existNode is JsonArray existArray)
                existArray.Add(node.DeepClone());
            else
            {
                JsonArray newArray = [existNode!.DeepClone(), node.DeepClone()];
                Extensions[property.Name] = newArray;
            }
        }
        else
        {
            Extensions[property.Name] = node;
        }
        return this;
    }

    /// <summary>
    /// Set the property with type and return itself
    /// </summary>
    public PropertyOwner SetProperty<TK, TV>(TV value) where TK : Property<TV>, new()
    {
        IProperty property = Activator.CreateInstance<TK>();
        property.SetValue(value);
        return SetProperty(property);
    }
    
    /// <summary>
    /// Clear the property value
    /// </summary>
    public PropertyOwner ClearProperty<T>() where T : class, IProperty
    {
        if (Extensions == null) return this;
        Extensions.Remove(typeof(T).GetPropertyName());
        return this;
    }

    /// <summary>
    /// Sets the property with the given property type and value
    /// </summary>
    public PropertyOwner SetProperty<T>(Type type, T value)
    {
        if (Activator.CreateInstance(type) is not IProperty prop) return this;
        prop.SetValue(value);
        return SetProperty(prop);
    }

    /// <summary>
    /// Gets the property by type. If the property is stackable, it will return the first value. Use GetProperties method to get all values for stackable properties.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public IProperty? GetProperty(Type type)
    {
        if (Extensions == null || !Extensions.TryGetValue(type.GetPropertyName(), out JsonNode? node)) return null;
        if (Activator.CreateInstance(type) is not IProperty prop) return null;
        if (prop.Stackable && node is JsonArray array)
        {
            if (array.Count == 0) return null;
            prop.SetValue(array.First()); // for stackable property, we only return the first value, and the rest values can be get by GetProperties method
        }
        else
            prop.SetValue(node);
        return prop;
    }

    /// <summary>
    /// Gets the property by type
    /// </summary>
    public T? GetProperty<T>() where T : class, IProperty => GetProperty(typeof(T)) as T;

    /// <summary>
    /// Gets the properties by type. If the property is stackable, it will return all the values as a collection. Otherwise, it will return a single value.
    /// </summary>
    public IEnumerable<IProperty> GetProperties(Type type)
    {
        if (Extensions == null || !Extensions.TryGetValue(type.GetPropertyName(), out JsonNode? node)) yield break;
        if (Activator.CreateInstance(type) is not IProperty prop) yield break;

        if (prop.Stackable && node is JsonArray array)
        {
            foreach (JsonNode? child in array)
            {
                if (child == null || child.IsEmpty()) continue;
                IProperty p = (Activator.CreateInstance(type) as IProperty)!;
                p.SetValue(child);
                if (p.HasValue)
                    yield return p;
            }

            yield break;
        }

        // default
        prop.SetValue(node);
        if (prop.HasValue)
            yield return prop;
    }

    /// <summary>
    /// Gets the property by type
    /// </summary>
    public IEnumerable<T> GetProperties<T>() where T : class, IProperty => GetProperties(typeof(T)).OfType<T>();

    /// <summary>
    /// Gets properties from the given types. The properties will be returned in the order of the given types.
    /// </summary>
    public IEnumerable<IProperty> GetProperties(IEnumerable<Type> types)
    {
        HashSet<Type> unStackableProperties = [];
        return from type in types
               from prop in GetProperties(type).Where(p => p.HasValue)
               where prop.Stackable || unStackableProperties.Add(prop.GetType())
               select prop;
    }

    /// <summary>
    /// CombineProperties other extensible properties into this instance. If there are duplicate keys, the values from the other instance will overwrite the existing values.
    /// </summary>
    public PropertyOwner CombineProperties(PropertyOwner? other, ISchemaRuntime? runtime = null, string? kind = null)
    {
        if (other?.Extensions is not { Count: > 0 }) return this;

        // try fetch the schema kind from the type
        if (runtime != null) kind ??= SchemaKind;
        
        if (Extensions == null || Extensions.Count == 0)
        {
            Extensions = other.Extensions.DeepClone() as JsonObject;
        }
        else if (runtime == null || kind is null)
        {
            Extensions ??= [];
            foreach (var (key, value) in other.Extensions)
                Extensions[key] = Combine(value, Extensions[key]);
        }
        else
        {
            // CombineProperties the properties
            HashSet<string> handled = new (StringComparer.OrdinalIgnoreCase);
            foreach (Type propType in runtime.GetSchemaKindPropertyTypes(kind))
            {
                handled.Add(propType.Name);
                IProperty[] otherProps = other.GetProperties(propType).ToArray();
                if (otherProps.Length == 0) continue;

                IProperty otherProp = otherProps.First();
                if (otherProp.Stackable)
                {
                    bool changed = false;
                    List<IProperty> existProps = GetProperties(propType).ToList();
                    foreach (IProperty s in otherProps)
                    {
                        if (existProps.Any(e => e.Equals(s))) continue; // skip if already exist
                        changed = true;
                        existProps.Add(s);
                    }
                    if (changed)
                        OverrideProperties(existProps);
                }
                else
                {
                    IProperty? existProp = GetProperty(propType);
                    if (existProp is { HasValue: true })
                    {
                        if (existProp.Combine(otherProp, runtime))
                            OverrideProperty(existProp);
                    }
                    else
                        OverrideProperty(otherProp);
                }
            }
            
            // for other un-supported properties
            foreach (KeyValuePair<string, JsonNode?> pair in other.Extensions)
            {
                if (!handled.Add(pair.Key) || Extensions.ContainsKey(pair.Key) || pair.Value is null || pair.Value.IsEmpty()) continue;
                Extensions[pair.Key] = pair.Value.DeepClone();
            }
        }

        return this;

        void OverrideProperty(IProperty prop)
        {
            Extensions?.Remove(prop.Name);
            SetProperty(prop);
        }

        void OverrideProperties(IEnumerable<IProperty> props)
        {
            bool first = true;
            foreach (IProperty prop in props)
            {
                if (first)
                {
                    Extensions?.Remove(prop.Name);
                    first = false;
                }
                SetProperty(prop);
            }
        }

        JsonNode? Combine(JsonNode? from, JsonNode? to)
        {
            if (to == null || to.IsEmpty() || to is not JsonObject toObject) return from?.DeepClone();
            if (from is not JsonObject fromObject) return toObject;
            foreach (var (key, value) in fromObject)
            {
                if (value != null && !value.IsEmpty())
                    toObject[key] = Combine(value, toObject.TryGetValue(key, out JsonNode? child) ? child : null);
            }
            return toObject;
        }
    }

    #endregion
}