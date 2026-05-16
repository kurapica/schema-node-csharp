using System.Text.Json;
using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Property.Schema;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;
using JsonNode = System.Text.Json.Nodes.JsonNode;

namespace SchemaNode.Scalar;

/// <summary>
/// Represents the object value type — any JSON value. The actual type is resolved by Relation at runtime.
/// </summary>
[Meta<ClrEquivalent>(typeof(JsonElement))]
[Meta<ClrEquivalent>(typeof(JsonNode))]
[Meta<ClrEquivalent>(typeof(JsonValue))]
[Meta<ClrEquivalent>(typeof(JsonArray))]
[Meta<ClrEquivalent>(typeof(JsonObject))]
[Meta<ClrEquivalent>(typeof(Node.IDataNode))]
[Meta<SchemaType>(NS_SYSTEM_OBJECT)]
[Meta<OfSchema>(SCHEMA_KIND_OBJECT)]
public class Object: IScalarType<object>;