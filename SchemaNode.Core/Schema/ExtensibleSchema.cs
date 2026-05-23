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
                IProperty? otherProp = other.GetProperty(propType);
                if (otherProp is not { HasValue: true }) continue;
                
                IProperty? existProp = GetProperty(propType);
                if (existProp is { HasValue: true })
                {
                    if (otherProp.GetValue<ExtensibleSchema>(true) is { } innerSchema)
                    {
                        ExtensibleSchema? existSchema = existProp.GetValue<ExtensibleSchema>(true);
                        if (existSchema == null)
                        {
                            SetProperty(otherProp);
                            continue;
                        }
                        
                        existSchema.CombineExtensions(innerSchema, runtime);
                        existProp.SetValue(existSchema);
                        SetProperty(existProp);
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
                
                SetProperty(otherProp);
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

    /// <summary>
    /// Gets properties from the given types. The properties will be returned in the order of the given types. If there are duplicate properties, the properties from the later types will overwrite the previous ones. If a property has dependencies, it will only be returned when all its dependencies are satisfied. If a property has overrides, it will override the properties with the same name from the previous types.
    /// </summary>
    public List<IProperty> GetProperties(IEnumerable<Type> types)
    {
        List<IProperty> props = [];
        foreach (Type type in types)
        {
            IProperty? prop = GetProperty(type);
            if (prop is not { HasValue: true }) continue;
            if (prop.Depends is { Length: > 0 } depends && depends.Any(d => props.All(p => !p.Name.Equals(d, StringComparison.OrdinalIgnoreCase)))) continue;
            if (prop.Overrides is { Length: > 0 } overrides) props = props.Where(p => !overrides.Any(o => o.Equals(p.Name, StringComparison.OrdinalIgnoreCase))).ToList();
            props.Add(prop);
        }
        return props;
    }

    #endregion
}