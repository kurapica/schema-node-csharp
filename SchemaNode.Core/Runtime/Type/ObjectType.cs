using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Schema;
using System.Text.Json.Nodes;
using static SchemaNode.Utility.Constant;
using JsonNode = System.Text.Json.Nodes.JsonNode;

namespace SchemaNode.Runtime;

/// <summary>
/// Runtime type for the "object" schema kind.
/// Accepts any JSON value; the actual semantic type is resolved by a Relation at runtime.
/// </summary>
public sealed class ObjectType : ScalarType
{
    protected override string? GetSchemaBase(NodeSchema schema) => null;

    public override async Task<(Node.DataNode? value, JsonNode? error)> ValidateValueAsync(
        SchemaContext context, JsonNode value, IReadOnlyList<IConstraintProperty>? constraints = null)
    {
        await Task.Yield();
        if (value == null)
            return (null, TYPE_VALUE_NOT_VALID);

        var result = new ScalarNode(this) { Value = value };

        if (!await ApplyConstraints(context, result, constraints))
            return (null, TYPE_VALUE_NOT_VALID);

        return (result, null);
    }
}
