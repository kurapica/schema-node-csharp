using SchemaNode.Property;
using SchemaNode.Property.Core;
using SchemaNode.Utility;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Property.Common;
using SchemaNode.Runtime;
using SchemaKind =  SchemaNode.Property.Record.SchemaKind;
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable AccessToModifiedClosure

namespace SchemaNode.Schema;

/// <summary>
/// The extensible class for all schemas that support extension properties. 
/// It is recommended to inherit from this class for all schemas that support extension properties, 
/// so that the extension properties can be easily added and combined.
/// </summary>
public abstract class ExtensibleSchema : IPropertyOwner
{
    [SchemaIgnore]
    [JsonIgnore]
    public string? SchemaKind => GetType().GetMetaProperty<SchemaKind>()?.GetValue<string>();

    /// <summary>
    /// The error status
    /// </summary>
    [Meta<SchemaType>(typeof(ErrorCode))]
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
    public virtual void CombineExtensions(ExtensibleSchema? other, ISchemaRuntime? runtime = null)
    {
        if (other?.Extensions is not { Count: > 0 } || !other.GetType().IsAssignableTo(GetType())) return;

        string? kind = SchemaKind;
        if (Extensions == null || Extensions.Count == 0 || runtime == null || kind is null)
        {
            Extensions ??= [];
            foreach (var (key, value) in other.Extensions)
                Extensions[key] = Combine(value, Extensions.GetValueOrDefault(key));
        }
        else
        {
            foreach (Type propType in runtime.GetSchemaKindProperties(kind))
            {
                IProperty[] otherProps = other.GetProperties(propType).ToArray();
                if (otherProps.Length == 0) continue;
                
                IProperty otherProp = otherProps.First();
                if (otherProp.Stackable)
                {
                    // keep using the latest
                    OverrideProperties(otherProps);
                }
                else
                {
                    IProperty? existProp = GetProperty(propType);
                    if (existProp is { HasValue: true })
                    {
                        if (otherProp.GetValue<ExtensibleSchema>(true) is { } innerSchema)
                        {
                            ExtensibleSchema? existSchema = existProp.GetValue<ExtensibleSchema>(true);
                            if (existSchema == null)
                            {
                                OverrideProperty(otherProp);
                                continue;
                            }
                            
                            existSchema.CombineExtensions(innerSchema, runtime);
                            existProp.SetValue(existSchema);
                            OverrideProperty(existProp);
                            continue;
                        }
                        else if (otherProp.GetValue<IEnumerable<ExtensibleSchema>>(true) is { } innerEnumerable)
                        {
                            if (existProp.GetValue<IEnumerable<ExtensibleSchema>>() is not { } existEnumerable)
                            {
                                SetProperty(otherProp);
                                continue;
                            }
                            
                            List<ExtensibleSchema> resultList = existEnumerable.ToList();
                            foreach (ExtensibleSchema combine in innerEnumerable.ToList())
                            {
                                ExtensibleSchema? match = resultList.FirstOrDefault(e => e.Equals(combine));
                                if (match != null)
                                    match.CombineExtensions(combine, runtime);
                                else
                                    resultList.Add(combine);
                            }

                            existProp.SetValue(resultList.ToArray());
                            SetProperty(existProp);
                            continue;
                        }
                    }
                }

                
                OverrideProperty(otherProp);
            }
        }

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

    /// <summary>
    /// Equal check
    /// </summary>
    public virtual bool Equals(ExtensibleSchema? other) => other != null && ReferenceEquals(this, other);
    
    #endregion

    #region Implementation of IPropertyOwner

    /// <inheritdoc/>
    public IProperty? GetProperty(Type type)
    {
        if (Extensions == null || !Extensions.TryGetValue(type.GetPropertyName(), out JsonNode? node)) return null;
        IProperty? prop = Activator.CreateInstance(type) as IProperty;
        if (prop == null) return null;
        if (prop.Stackable && node is JsonArray array)
            prop.SetValue(array.FirstOrDefault()); // for stackable property, we only return the first value, and the rest values can be get by GetProperties method
        else
            prop.SetValue(node);
        return prop;
    }

    /// <inheritdoc/>
    public void SetProperty(IProperty property)
    {
        Extensions ??= [];
        JsonNode? node = property.GetValue<JsonNode>();
        if (node == null) return;
        
        // Just keep in mind the stackable property not use array value types, maybe we will allow that in the future
        if (property.Stackable && Extensions.TryGetValue(property.Name, out JsonNode? existNode) && !existNode.IsEmpty())
        {
            if (existNode is JsonArray existArray)
            {
                existArray.Add(node.DeepClone());
            }
            else
            {
                JsonArray newArray =
                [
                    existNode.DeepClone(),
                    node.DeepClone()
                ];
                Extensions[property.Name] = newArray;
            }
        }
        else
        {
            Extensions[property.Name] = node;
        }
    }
    
    /// <inheritdoc/>
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

    #endregion
}