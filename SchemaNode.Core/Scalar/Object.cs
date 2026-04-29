using System.Text.Json;
using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;
using JsonNode = System.Text.Json.Nodes.JsonNode;

namespace SchemaNode.Scalar;

/// <summary>
/// Represents the object value type, which can be any value, including string, number, boolean, null, array and object.
/// </summary>
[Meta<ClrEquivalent>(typeof(JsonElement))]
[Meta<ClrEquivalent>(typeof(JsonNode))]
[Meta<ClrEquivalent>(typeof(JsonValue))]
[Meta<ClrEquivalent>(typeof(JsonArray))]
[Meta<ClrEquivalent>(typeof(JsonObject))]
[Meta<ClrEquivalent>(typeof(Node.DataNode))]
[Meta<SchemaType>(NS_SYSTEM_OBJECT)]
public class Object: IScalarType<object>;