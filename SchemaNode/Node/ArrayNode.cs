using System.Text.Json;
using System.Text.Json.Nodes;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Node;

/// <summary>
/// The in-memory array schema representation
/// </summary>
public class ArrayNode: AnySchemaNode
{
    #region Data
    
    /// <summary>
    /// The element type of the array.
    /// </summary>
    public string? Element { get; set; }

    /// <summary>
    /// Whether the array should be treated as a whole value,
    /// no element schema nodes would be created
    /// </summary>
    public bool? Single { get; set; }

    /// <summary>
    /// The primary fields of the array if the element is a struct.
    /// </summary>
    public string[]? Primary { get; set; }

    /// <summary>
    /// The data combine rule
    /// </summary>
    public DataCombine[]? Combine { get; set; }

    /// <summary>
    /// The realtions between the fields
    /// </summary>
    public StructFieldRelation[]? Relations { get; set; }
    
    /// <summary>
    /// The additional data
    /// </summary>
    public Dictionary<string, JsonElement>? Additional { get; set; }
    
    #endregion
    
    #region Status
    
    /// <inheritdoc />
    public override SchemaType Type => SchemaType.Array;
    
    #endregion
    
    #region Ref
    
    /// <summary>
    /// The element type node
    /// </summary>
    public AnySchemaNode? ElementNode { get; set; }
    
    #endregion
    
    #region Method

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        ArraySchema? array = schema.Array;
        
        // Data
        Element = array?.Element;
        Single = array?.Single;
        Primary = array?.Primary;
        Combine = array?.Combine;
        Relations = array?.Relations;
        Additional = array?.Additional;
        
        // Status
        if (array == null) Status = SchemaNodeStatus.NoDefinition;
        
        // Ref
        if (!string.IsNullOrWhiteSpace(Element))
        {
            AnySchemaNode? node = await context.GetSchemaNodeAsync(Element, preload);
            if (node == null || node.Type is SchemaType.Namespace or SchemaType.Array or SchemaType.Function)
            {
                Status = SchemaNodeStatus.ArrayHasWrongElementType;
            }
            else
            {
                ElementNode = node;
                node.AddRef(this);
            }
        }
        
        // Relation
        if (Relations != null)
        {
            foreach (StructFieldRelation relation in Relations)
            {
                AnySchemaNode? node = await context.GetSchemaNodeAsync(relation.Func, preload);
                if (node is not FunctionNode funcNode)
                {
                    Status = SchemaNodeStatus.StructRelationshipWrongFunc;
                    continue;
                }
                relation.FuncNode = funcNode;
                funcNode.AddRef(this);
            }
        }
    }

    /// <inheritdoc />
    public override void Release()
    {
        ElementNode?.RemoveRef(this);
        ElementNode = null;

        if (Relations != null)
        {
            foreach (StructFieldRelation relation in Relations)
            {
                relation.FuncNode?.RemoveRef(this);
                relation.FuncNode = null;
            }
        }
    }

    /// <inheritdoc />
    public override async Task<(JsonNode? value, JsonNode? error)> ValidateValueAsync(SchemaContext context, JsonNode value)
    {
        if (value is not JsonArray array)
            return (value, TYPE_VALUE_NOT_VALID);
        
        if (ElementNode == null)
            return (array, null);
        
        // validate elements
        JsonArray result = [];
        JsonObject? error = null;
        for (int i = 0; i < array.Count; i++)
        {
            (JsonNode? v, JsonNode? e) = await ElementNode.ValidateValueAsync(context, array[i]!);
            if (e != null && !e.IsEmpty())
            {
                error ??= new JsonObject();
                error[i.ToString()] = e;
            }
            else
            {
                result.Add(v);
            }
        }
        
        // @TODO Union Validation
        
        return (result, error);
    }

    /// <inheritdoc />
    public override bool CanBeUseAs(AnySchemaNode other) => 
        this == other 
        || Name.Equals(NS_SYSTEM_ARRAY) 
        || other.Name.Equals(NS_SYSTEM_ARRAY) 
        || (other is ArrayNode array && ElementNode != null && array.ElementNode != null && ElementNode.CanBeUseAs(array.ElementNode));

    /// <inheritdoc />
    public override ArrayNode? GetArrayNode(bool exactly = false) => null;

    #endregion

    #region Conversion
    
    /// <summary>
    /// Convert the node to schema
    /// </summary>
    public static implicit operator NodeSchema?(ArrayNode? schema)
    {
        if (schema == null) return null;
        return new NodeSchema
        {
            Name = schema.Name,
            Type = schema.Type,
            Display = schema.Display,
            LoadState = schema.LoadState,
            Array = new ArraySchema
            {
                Element = schema.Element,
                Single = schema.Single,
                Primary = schema.Primary,
                Combine = schema.Combine,
                Relations = schema.Relations,
                Additional = schema.Additional,
            }
        };
    }
    
    #endregion
}
