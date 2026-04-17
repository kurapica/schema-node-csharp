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
public abstract class ExtensibleSchema: IPropertyOwner
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
    [NotMapped]
    [JsonExtensionData]
    public Dictionary<string, JsonNode>? Extensions { get; private set; }

    /// <summary>
    /// Combine other extensible properties into this instance. If there are duplicate keys, the values from the other instance will overwrite the existing values.
    /// </summary>
    /// <param name="other"></param>
    public void CombineExtensions(ExtensibleSchema? other)
    {
        if (other?.Extensions is not { Count: > 0 } || !other.GetType().IsAssignableTo(GetType())) return;
        
        Extensions ??= [];
        foreach (var (key, value) in other.Extensions)
            Extensions[key] = value;
    }
    
    #endregion

    #region Implementation of IPropertyOwner

    public T? GetProperty<T>() where T : IProperty, new()
    {
        if (Extensions == null) return default(T?);
        string key = GetPropertyName<T>();
        if (!Extensions.TryGetValue(key, out JsonNode? node)) return default(T?);
        IProperty prop = Activator.CreateInstance<T>();
        prop.SetValue(node);
        return (T?)prop;
    }

    public void SetProperty<T>(T property) where T : IProperty
    {
        Extensions ??= [];
        string key = GetPropertyName<T>();
        JsonNode? node = property.GetValue<JsonNode>();
        if (node != null)
            Extensions[key] = node;
    }

    public void RemoveProperty<T>(T property) where T : IProperty
    {
        if (Extensions == null) return;
        string key = GetPropertyName<T>();
        Extensions.Remove(key);
    }

    string GetPropertyName<T>() where T : IProperty
    {
        Type type = typeof(T);
        return type.GetMetaProperty<Alias>()?.Value ?? type.Name.GetPropertyName();
    }
    
    #endregion
}