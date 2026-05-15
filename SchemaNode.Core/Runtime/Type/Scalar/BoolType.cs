using SchemaNode.Node;
using SchemaNode.Utility;

namespace SchemaNode.Runtime;

/// <summary>
/// Runtime type for the "bool" schema kind.
/// </summary>
public sealed class BoolType : ScalarType
{
    /// <inheritdoc />
    public override bool IsIndexable => true;

    /// <inheritdoc />
    public override DataNode ParseValue(object? value)
        =>  value is BoolNode node && node.Type == this ? node : new BoolNode(this, value is bool bVal || TryParseBoolValue(value?.TryConvertTo<string>(), out bVal) ? bVal : null);

    // Parses a string to a bool (accepts "true"/"false"/0/1)
    static bool TryParseBoolValue(string? value, out bool ret)
    {
        ret = false;
        if (string.IsNullOrEmpty(value)) return false;
        value = value.ToLower();
        switch (value)
        {
            case "true":  ret = true;  return true;
            case "false": ret = false; return true;
            default:
                if (!int.TryParse(value, out int val) || val is < 0 or > 1) return false;
                ret = val == 1;
                return true;
        }
    }

}
