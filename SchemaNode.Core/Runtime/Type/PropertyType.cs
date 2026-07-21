using SchemaNode.Context;
using SchemaNode.Property;
using SchemaNode.Schema;
using SchemaNode.Utility;

namespace SchemaNode.Runtime;

public class PropertyType : NodeType
{
    private PropertySchema? _property;

    /// <summary>
    /// The property
    /// </summary>
    public string Property => _property?.Property ?? string.Empty;
    
    /// <summary>
    /// The property value type
    /// </summary>
    public ValueType? ValueType { get; private set; }

    /// <inheritdoc/>
    public override async Task LoadAsync(SchemaContext context)
    {
        _property = GetProperty<Schema.PropertyProperty>()?.Value;
        ValueType = !string.IsNullOrWhiteSpace(_property?.Type) 
            ? await context.GetNodeTypeAsync<ValueType>(_property.Type)
            : null;
        if (ValueType == null)
            Error = ErrorCodes.NO_DEFINITION;
    }

    /// <inheritdoc/>
    public override IEnumerable<NodeType> GetReferenceTypes()
    {
        if (ValueType != null)
            yield return ValueType;
        foreach(var type in base.GetReferenceTypes())
            yield return type;
    }
    
    /// <summary>
    /// Gets the property with the given type
    /// </summary>
    public new T? GetProperty<T>() where T : class, IProperty 
        => base.GetProperty<T>() ?? Runtime?.GetSchemaKindProperty<T>(Kind);

    /// <summary>
    /// Gets the properties with the given type
    /// </summary>
    public new IEnumerable<T> GetProperties<T>() where T : class, IProperty
        => this.JoinProperties(base.GetProperties<T>(), Runtime?.GetSchemaKindProperties<T>(Kind));
}