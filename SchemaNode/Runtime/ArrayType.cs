using System.Text.Json;
using System.Text.Json.Nodes;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory array schema representation
/// </summary>
public class ArrayType: AnySchemeType
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
    /// The indexes
    /// </summary>
    public DataIndex[]? Indexes { get; set; }

    /// <summary>
    /// The data combine rule
    /// </summary>
    public DataCombine[]? Combines { get; set; }

    /// <summary>
    /// The relation between the fields
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
    public AnySchemeType? ElementNode { get; set; }
    
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
        Combines = array?.Combines;
        Relations = array?.Relations;
        Indexes = array?.Indexes;
        Additional = array?.Additional;
        
        // Status
        if (array == null) Status = SchemaNodeStatus.NoDefinition;
        
        // Ref
        if (!string.IsNullOrWhiteSpace(Element))
        {
            AnySchemeType? node = await context.GetSchemaNodeAsync(Element, preload: preload);
            if (node == null || node.Type is SchemaType.Namespace or SchemaType.Array or SchemaType.Func)
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
                AnySchemeType? node = await context.GetSchemaNodeAsync(relation.Func, preload: preload);
                if (node is not FunctionType funcNode)
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
    public override async Task<(AnySchemaNode? value, JsonNode? error)> ValidateValueAsync(SchemaContext context, JsonNode value)
    {
        if (value is not JsonArray array)
            return (null, TYPE_VALUE_NOT_VALID);

        // validate elements
        ArrayNode result = new(this);
        JsonObject? error = null;
        if (ElementNode != null)
        {
            for (int i = 0; i < array.Count; i++)
            {
                (AnySchemaNode? v, JsonNode? e) = await ElementNode.ValidateValueAsync(context, array[i]!);
                if (e != null && !e.IsEmpty())
                {
                    error ??= new JsonObject();
                    error[i.ToString()] = e;
                }
                else if (v != null)
                {
                    result.Add(v);
                }
            }
        }
        else
        {
            result.AddRange(array);
        }

        // @TODO Union Validation
        return (result, error);
    }

    /// <inheritdoc />
    public override bool CanBeUseAs(AnySchemeType other) => 
        this == other 
        || Name.Equals(NS_SYSTEM_ARRAY) 
        || other.Name.Equals(NS_SYSTEM_ARRAY) 
        || (other is ArrayType array && ElementNode != null && array.ElementNode != null && ElementNode.CanBeUseAs(array.ElementNode));

    /// <inheritdoc />
    public override ArrayType? GetArrayNode(bool exactly = false) => null;

    /// <summary>
    /// Get unique key for object
    /// </summary>
    public string? GetPrimaryKey(JsonObject obj)
    {
        if (Primary == null || Primary.Length == 0 || ElementNode is not StructType { Fields.Length: > 0 } @struct)
            return null;

        string? key = null;
        foreach (string p in Primary)
        {
            if (obj.ContainsKey(p))
            {
                StructFieldConfig? fld = @struct.Fields.FirstOrDefault(f => f.Name.Equals(p));
                if (fld == null) return null;
                string part = fld.TypeNode is ScalarType { IsDate: true } ? $"{obj[p]!.GetValue<DateTime>().FromUtc():yyyyMMdd}" : $"{obj[p]}";
                key = string.IsNullOrWhiteSpace(key) ? part : $"{key}^{part}";
            }
            else
            {
                return null;
            }
        }
        return key;
    }

    /// <summary>
    /// Get unique key for object
    /// </summary>
    public string? GetPrimaryKey(StructNode obj)
    {
        if (Primary == null || Primary.Length == 0 || ElementNode is not StructType { Fields.Length: > 0 } @struct)
            return null;

        string? key = null;
        foreach (string p in Primary)
        {
            StructFieldConfig? fld = @struct.Fields.FirstOrDefault(f => f.Name.Equals(p));
            if (fld == null) return null;
            string part = fld.TypeNode is ScalarType { IsDate: true } ? $"{obj.GetField(p)!.ToValue<DateTime>().FromUtc():yyyyMMdd}" : $"{obj[p]}";
            key = string.IsNullOrWhiteSpace(key) ? part : $"{key}^{part}";
        }
        return key;
    }

    public override IEnumerable<AnySchemeType> GetDependNodes()
    {
        if (ElementNode != null)
            yield return ElementNode;
        
        if (Relations != null)
        {
            foreach (StructFieldRelation relation in Relations)
            {
                if (relation.FuncNode != null)
                    yield return relation.FuncNode;
            }
        }
    }

    #endregion

    #region Conversion
    
    /// <summary>
    /// Convert the node to schema
    /// </summary>
    public static implicit operator NodeSchema?(ArrayType? schema)
    {
        if (schema == null) return null;
        return new NodeSchema
        {
            Name = schema.Name,
            Type = schema.Type,
            Display = schema.Display,
            LoadState = schema.LoadState,
            Used = schema.IsUsed,
            UsedBy = schema.UsedBy?.Keys.Select(p => p.Name).ToArray(),
            UsedByApp = schema.UsedByApp?.Keys.Select(p => p.App).Distinct().ToArray(),
            Array = new ArraySchema
            {
                Element = schema.Element,
                Single = schema.Single,
                Primary = schema.Primary,
                Combines = schema.Combines,
                Relations = schema.Relations,
                Additional = schema.Additional,
            }
        };
    }
    
    #endregion
}
