using SchemaNode.Context;
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
        _property = GetProperty<Schema.Property>()?.Value;
        ValueType = !string.IsNullOrWhiteSpace(_property?.Type) 
            ? await context.GetNodeTypeAsync<ValueType>(_property.Type)
            : null;
        if (ValueType == null)
            Error = ErrorCodes.NO_DEFINITION;
    }
}