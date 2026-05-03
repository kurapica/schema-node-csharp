using SchemaNode.Context;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Schema;
using System.Text.Json.Nodes;
using static SchemaNode.Utility.Extension;
using static SchemaNode.Utility.Constant;
using JsonNode = System.Text.Json.Nodes.JsonNode;

namespace SchemaNode.Runtime;

/// <summary>
/// Runtime type for the "int" schema kind (Int, Year).
/// Year is validated as either a plain long integer or extracted from a date string.
/// </summary>
public sealed class IntType : ScalarType
{
    protected override string? GetSchemaBase(NodeSchema schema) =>
        schema.GetProperty<IntProperty>()?.Value?.Base;

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
            if (IsYear)
            {
                if (long.TryParse(strVal, out long year))
                {
                    // plain integer year — pass through
                }
                else if (TryParseDateTimeOffset(strVal, out DateTimeOffset? dateTime))
                {
                    year = SystemCalendar.getyear(context, dateTime!.Value);
                }
                else
                {
                    return (null, TYPE_VALUE_NOT_VALID);
                }
                result.Value = year;
            }
            else
            {
                if (!long.TryParse(strVal, out long lval))
                    return (null, TYPE_VALUE_NOT_VALID);
                result.Value = lval;
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
