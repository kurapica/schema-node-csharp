using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Convert;

/// <summary>
/// Inline enum-to-display mapping. Each Entry.Value matches an enum value,
/// and Entry.Label provides the display string with optional localization.
/// When emitting, the enum value is replaced by the matching Entry's label key.
/// When parsing, the display string is mapped back to the enum value.
/// </summary>
[SchemaProperty([SchemaType.RecognizerPart], schemaType: NS_SYSTEM_ENTRIES)]
public class MappingProperty : SchemaProperty<Entry[]>, IConvertProperty
{
    private readonly Dictionary<string, string> _displayToValue = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _valueToDisplay = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public override void Init(SchemaContext context)
    {
        if (Value == null) return;

        foreach (var entry in Value)
        {
            string displayKey = entry.Label?.Key ?? entry.Value;

            _displayToValue.TryAdd(displayKey, entry.Value);
            _valueToDisplay.TryAdd(entry.Value, displayKey);

            if (entry.Label?.Trans != null)
            {
                foreach (var tran in entry.Label.Trans)
                {
                    if (!string.IsNullOrEmpty(tran.Tran))
                        _displayToValue.TryAdd(tran.Tran, entry.Value);
                }
            }
        }
    }

    /// <inheritdoc/>
    public string? Parse(SchemaContext context, string value, string result, AnySchemaNode? overrideValue = null)
    {
        string raw = result ?? value;
        return _displayToValue.TryGetValue(raw, out var enumValue) ? enumValue : null;
    }

    /// <inheritdoc/>
    public string? Emit(SchemaContext context, string value, string? result, AnySchemaNode? overrideValue = null)
    {
        string raw = result ?? value;
        return _valueToDisplay.TryGetValue(raw, out var display) ? display : raw;
    }
}
