using SchemaNode.Context;
using SchemaNode.Schema;
using SchemaNode.Utility;

namespace SchemaNode.Runtime;

public class PropertyType : NodeType
{
    private PropertySchema? _property;
    
    /// <summary>
    /// The property value type
    /// </summary>
    public ValueType? ValueType { get; private set; }

    /// <inheritdoc/>
    public override async Task LoadAsync(SchemaContext context)
    {
        _property = GetProperty<PropProperty>()?.Value;
        ValueType = !string.IsNullOrWhiteSpace(_property?.Type) 
            ? await context.GetNodeTypeAsync<ValueType>(_property.Type)
            : null;
        if (ValueType == null)
            Error = ErrorCodes.NO_DEFINITION;
    }
}