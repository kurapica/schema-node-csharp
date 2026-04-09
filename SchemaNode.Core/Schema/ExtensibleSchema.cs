using SchemaNode.Property;
using SchemaNode.Property.Schema;
using SchemaNode.Utility;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace SchemaNode.Schema;

/// <summary>
/// The extensible class for all schemas that support extension properties. 
/// It is recommended to inherit from this class for all schemas that support extension properties, 
/// so that the extension properties can be easily added and combined.
/// </summary>
public abstract class ExtensibleSchema: IPropertyOwner
{
    /// <summary>
    /// The list of properties declared by the schema
    /// </summary>
    [JsonIgnore]
    public List<IProperty>? Properties { get; set; }

    /// <summary>
    /// The dictionary to hold the extension properties. The key is the property name, and the value is the property value.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonNode>? Extensions { get; private set; }

    /// <summary>
    /// Combine other extensible properties into this instance. If there are duplicate keys, the values from the other instance will overwrite the existing values.
    /// </summary>
    /// <param name="other"></param>
    public void CombineExtensions(ExtensibleSchema? other)
    {
        if (other?.Extensions is not { Count: > 0 }) return;

        Extensions ??= [];
        foreach (var (key, value) in other.Extensions)
            Extensions[key] = value;
    }

    public IEnumerable<IProperty> GetAllProperties()
    {
        throw new NotImplementedException();
    }

    public IProperty? GetProperty<T>() where T : IProperty
    {
        throw new NotImplementedException();
    }

    public void RemoveProperty<T>() where T : IProperty
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Sets the value of a custom property identified by the specified key.
    /// </summary>
    /// <remarks>If a property with the specified key already exists, its value is overwritten.</remarks>
    /// <typeparam name="T">The type of the value to associate with the property key.</typeparam>
    /// <param name="key">The key that identifies the property to set. Cannot be null.</param>
    /// <param name="value">The value to assign to the property. May be null to remove or clear the property.</param>
    public void SetProperty<T>(string key, T? value)
    {
        JsonNode? v = value?.ToJsonNode();
        if (v == null)
        {
            Extensions?.Remove(key);
            return;
        }
        Extensions ??= [];
        Extensions[key] = v;
    }

    /// <summary>
    /// Set the value of a custom property by the specified property type. The property name is determined by the PropertyDeclareAttribute on the property type, or the property type name if the attribute is not present.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="prop"></param>
    /// <param name="value"></param>
    public void SetProperty<T>(Type prop, T value) => SetProperty(prop.GetCustomAttribute<PropertyName>()?.Name ?? prop.Name.GetPropertyName(), value);

    /// <summary>
    /// Sets the value of a custom property by the specified property type
    /// </summary>
    /// <typeparam name="TK"></typeparam>
    /// <typeparam name="TV"></typeparam>
    /// <param name="value"></param>
    public void SetProperty<TK, TV>(TV? value) where TK: IProperty => SetProperty(typeof(TK), value);

    public void SetProperty<T>(T property) where T : IProperty
    {
        throw new NotImplementedException();
    }
}
