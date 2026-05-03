using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Schema;
using System.Text.Json.Nodes;
using static SchemaNode.Utility.Extension;
using static SchemaNode.Utility.Constant;
using JsonNode = System.Text.Json.Nodes.JsonNode;

namespace SchemaNode.Runtime;

/// <summary>
/// Runtime type for the "decimal" schema kind (Number, Double, Float / Single).
/// </summary>
public sealed class DecimalType : ScalarType
{
    protected override string? GetSchemaBase(NodeSchema schema) =>
        schema.GetProperty<DecimalProperty>()?.Value?.Base;

    public override async Task<(Node.DataNode? value, JsonNode? error)> ValidateValueAsync(
        SchemaContext context, JsonNode value, IReadOnlyList<IConstraintProperty>? constraints = null)
    {
        await Task.Yield();
        if (value is not JsonValue val || val.IsEmpty())
            return (null, TYPE_VALUE_NOT_VALID);

        string strVal = value.ToString();
        var result = new ScalarNode(this);
        try
        {
            if (IsSingle)
            {
                if (!float.TryParse(strVal, out float fval))
                    return (null, TYPE_VALUE_NOT_VALID);
                result.Value = fval;
            }
            else if (IsDouble)
            {
                if (!double.TryParse(strVal, out double dval))
                    return (null, TYPE_VALUE_NOT_VALID);
                result.Value = dval;
            }
            else
            {
                if (!decimal.TryParse(strVal, out decimal mval))
                    return (null, TYPE_VALUE_NOT_VALID);
                result.Value = mval;
            }

            if (!await ApplyConstraints(context, result, constraints))
                return (null, TYPE_VALUE_NOT_VALID);

            return (result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.GetInnermostException().Message);
        }
        return (null, TYPE_VALUE_NOT_VALID);
    }
}
