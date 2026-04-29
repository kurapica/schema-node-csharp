using System.ComponentModel.DataAnnotations.Schema;
using SchemaNode.Property;
using SchemaNode.Property.Schema;
using SchemaNode.Utility;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Property.Presentation;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/// <summary>
/// The extensible class for all schemas that support extension properties. 
/// It is recommended to inherit from this class for all schemas that support extension properties, 
/// so that the extension properties can be easily added and combined.
/// </summary>
public abstract class ExtensibleSchema : IPropertyOwner
{
    /// <summary>
    /// The error status
    /// </summary>
    [Meta<SchemaType>(NS_SYSTEM_SCHEMA_ERROR)]
    [Meta<ReadOnly>(true)]
    public string? Error { get; set; }

    #region Extensions

    /// <summary>
    /// The dictionary to hold the extension properties. The key is the property name, and the value is the property value.
    /// </summary>
    [SchemaIgnore]
    [JsonExtensionData]
    public Dictionary<string, JsonNode>? Extensions { get; private set; }

    /// <summary>
    /// Combine other extensible properties into this instance. If there are duplicate keys, the values from the other instance will overwrite the existing values.
    /// </summary>
    public void CombineExtensions(ExtensibleSchema? other, IEnumerable<Type>? propTypes = null)
    {
        if (other?.Extensions is not { Count: > 0 } || !other.GetType().IsAssignableTo(GetType())) return;

        if (Extensions == null || Extensions.Count == 0 || propTypes == null)
        {
            Extensions ??= [];
            foreach (var (key, value) in other.Extensions)
                Extensions[key] = Combine(value, Extensions.GetValueOrDefault(key));
        }
        else
        {
            foreach (Type propType in propTypes)
            {
                IProperty? otherProp = other.GetProperty(propType);
                if (otherProp is not { HasValue: true }) continue;
                Extensions ??= [];

                if (otherProp.GetValue<ExtensibleSchema>(true) is { } innerSchema)
                {
                    IProperty? innerProp = innerSchema.GetProperty(propType);
                    ExtensibleSchema? existSchema = innerProp?.GetValue<ExtensibleSchema>(true);
                    if (existSchema == null) continue;
                    existSchema.CombineExtensions(innerSchema);
                    innerProp!.SetValue(existSchema);
                    SetProperty(innerProp);
                }
                else
                {
                    SetProperty(otherProp);
                }
            }
        }

        JsonNode Combine(JsonNode from, JsonNode? to)
        {
            if (to == null || to.IsEmpty() || to is not JsonObject toObject) return from;
            if (from is not JsonObject fromObject) return toObject;
            foreach (var (key, value) in fromObject)
            {
                if (value != null && !value.IsEmpty())
                    toObject[key] = Combine(value, toObject.TryGetValue(key,  out JsonNode? child) ? child : null);
            }
            return toObject;
        }
    }

    #endregion

    #region Implementation of IPropertyOwner

    /// <inheritdoc/>
    public IProperty? GetProperty(Type type)
    {
        if (Extensions == null || !Extensions.TryGetValue(type.GetPropertyName(), out JsonNode? node)) return null;
        IProperty? prop = Activator.CreateInstance(type) as IProperty;
        prop?.SetValue(node);
        return prop;
    }

    /// <inheritdoc/>
    public void RemoveProperty(Type type) => Extensions?.Remove(type.GetPropertyName());

    /// <inheritdoc/>
    public void RemoveProperty<T>() where T : IProperty => RemoveProperty(typeof(T));

    /// <inheritdoc/>
    public T? GetProperty<T>() where T : IProperty, new()
    {
        if (Extensions == null || !Extensions.TryGetValue(typeof(T).GetPropertyName(), out JsonNode? node)) return default(T?);
        IProperty prop = Activator.CreateInstance<T>();
        prop.SetValue(node);
        return (T?)prop;
    }

    /// <inheritdoc/>
    public void SetProperty(IProperty property)
    {
        Extensions ??= [];
        JsonNode? node = property.GetValue<JsonNode>();
        if (node != null)
            Extensions[property.GetType().GetPropertyName()] = node;
    }

    /// <inheritdoc/>
    public void SetProperty<TK, TV>(TV? value) where TK : Property<TV>, new()
    {
        TK prop = Activator.CreateInstance<TK>();
        prop.SetValue(value);
        SetProperty(prop);
    }

    /// <inheritdoc/>
    public void SetProperty<T>(Type type, T value)
    {
        IProperty? prop = Activator.CreateInstance(type) as IProperty;
        if (prop == null) return;
        prop.SetValue(value);
        SetProperty(prop);
    }

    #endregion
}