using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using static SchemaNode.Utility.Constant;
using JsonNode = System.Text.Json.Nodes.JsonNode;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory array schema representation
/// </summary>
public sealed class ArrayType: ValueType
{
    #region Data
    
    /// <summary>
    /// The element type of the array.
    /// </summary>
    public string? Element { get; private set; }

    /// <summary>
    /// The primary fields of the array if the element is a struct.
    /// </summary>
    public string[]? Primary { get; private set; }

    /// <summary>
    /// The indexes
    /// </summary>
    public DataIndex[]? Indexes { get; private set; }

    /// <summary>
    /// The data combine rule
    /// </summary>
    public DataCombine[]? Combines { get; private set; }

    /// <summary>
    /// The relation between the fields
    /// </summary>
    public StructRelationSchema[]? Relations { get; private set; }

    /// <summary>
    /// The atomic flag indicates whether the array is atomic, which means that the array should be treated as a whole when performing operations such as updates, delete or render.
    /// </summary>
    public bool? Atomic { get; private set; }

    #endregion

    #region Status

    #endregion

    #region Ref

    /// <summary>
    /// The element type node
    /// </summary>
    public NodeType? ElementSchemaType { get; internal set; }
    
    #endregion
    
    #region Method

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        ArraySchema? array = schema.Array;
        
        // Data
        Element = array?.Element;
        Primary = array?.Primary;
        Combines = array?.Combines;
        Relations = array?.Relations;
        Indexes = array?.Indexes;
        Atomic = array?.Atomic;
        
        // Status
        if (array == null) Error = SchemaNodeStatus.NoDefinition;
        
        // Ref
        if (!string.IsNullOrWhiteSpace(Element))
        {
            NodeType? node = await context.GetSchemaTypeAsync(Element, preload: preload);
            if (node == null || node is not GenericType && node.Kind is NodeType.Namespace or NodeType.Array or NodeType.Func)
            {
                Error = SchemaNodeStatus.ArrayHasWrongElementType;
            }
            else
            {
                ElementSchemaType = node;
                node.AddUsedBy(this);
            }
        }
        
        // Relation
        if (Relations != null)
        {
            foreach (StructRelationSchema relation in Relations)
            {
                NodeType? node = await context.GetSchemaTypeAsync(relation.Func, preload: preload);
                if (node is not FunctionType funcNode)
                {
                    relation.Status = SchemaNodeStatus.StructRelationshipWrongFunc;
                    Error = SchemaNodeStatus.StructRelationshipWrongFunc;
                    continue;
                }
                relation.FuncNode = funcNode;
                funcNode.AddUsedBy(this);
            }
        }
    }

    /// <inheritdoc />
    public override void Release()
    {
        ElementSchemaType?.RemoveUsedBy(this);
        ElementSchemaType = null;

        if (Relations != null)
        {
            foreach (StructRelationSchema relation in Relations)
            {
                relation.FuncNode?.RemoveRef(this);
                relation.FuncNode = null;
            }
        }
    }

    /// <inheritdoc />
    public override async Task<Node.DataNode?> ValidateValueAsync(SchemaContext context, JsonNode value, IReadOnlyList<IConstraintProperty>? constraints = null)
    {
        if (value is not JsonArray array)
            return (null, TYPE_VALUE_NOT_VALID);

        // validate elements
        ArrayNode result = new(this);
        JsonObject? error = null;
        if (ElementSchemaType != null)
        {
            IConstraintProperty[] eleConstraints = Constraints?.Where(c => c.ForArrayOnly == false).ToArray() ?? [];
            if (constraints != null)
            {
                for(int i = 0; i < eleConstraints.Length; i++)
                {
                    if (constraints.FirstOrDefault(c => c.GetType() == eleConstraints[i].GetType()) is IConstraintProperty cst && cst.HasValue)
                        eleConstraints[i] = cst;
                }
            }
            eleConstraints = eleConstraints.Where(c => c.HasValue).ToArray();

            for (int i = 0; i < array.Count; i++)
            {
                (Node.DataNode? v, JsonNode? e) = await ElementSchemaType.ValidateValueAsync(context, array[i]!, eleConstraints);
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

        // Constraint validation
        if (Constraints is { Length: > 0 })
        {
            foreach (IConstraintProperty constraint in Constraints.Where(p => p.ForArrayOnly))
            {
                if (constraints != null && constraints.FirstOrDefault(c => c.GetType() == constraint.GetType()) is IConstraintProperty cst && cst.HasValue)
                {
                    if (await cst.ValidateArrayAsync(context, (ArrayNode)result) == false)
                        return (null, TYPE_VALUE_NOT_VALID);
                }
                else if (await constraint.ValidateArrayAsync(context, (ArrayNode)result) == false)
                    return (null, TYPE_VALUE_NOT_VALID);
            }
        }

        return (result, error);
    }

    /// <inheritdoc />
    public override bool CanBeUseAs(NodeType other, bool exactly = false) => 
        base.CanBeUseAs(other, exactly)
        || Name.Equals(NS_SYSTEM_ARRAY) 
        || other.Name.Equals(NS_SYSTEM_ARRAY) 
        || (other is ArrayType array && ElementSchemaType != null && array.ElementSchemaType != null && ElementSchemaType.CanBeUseAs(array.ElementSchemaType));

    /// <inheritdoc />
    public override ArrayType? GetArrayType(bool exactly = false) => null;

    /// <summary>
    /// Get unique key for object
    /// </summary>
    public string? GetPrimaryKey(JsonObject obj)
    {
        if (Primary == null || Primary.Length == 0 || ElementSchemaType is not StructType { Fields.Length: > 0 } @struct)
            return null;

        string? key = null;
        foreach (string p in Primary)
        {
            if (obj.ContainsKey(p))
            {
                StructFieldSchema? fld = @struct.Fields.FirstOrDefault(f => f.Name.Equals(p));
                if (fld == null) return null;
                string part = fld.SchemaType is ScalarType { IsDate: true } ? $"{obj[p]!.GetValue<DateTime>():yyyyMMdd}" : $"{obj[p]}";
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
        if (Primary == null || Primary.Length == 0 || ElementSchemaType is not StructType { Fields.Length: > 0 } @struct)
            return null;

        string? key = null;
        foreach (string p in Primary)
        {
            StructFieldSchema? fld = @struct.Fields.FirstOrDefault(f => f.Name.Equals(p));
            if (fld == null) return null;
            Node.DataNode? fieldNode = obj.GetField(p);
            if (fieldNode == null) return null;
            string part = fld.SchemaType is ScalarType { IsDate: true } ? $"{fieldNode.ToValue<DateTime>():yyyyMMdd}" : $"{fieldNode}";
            key = string.IsNullOrWhiteSpace(key) ? part : $"{key}^{part}";
        }
        return key;
    }

    public override IEnumerable<NodeType> GetDependNodes()
    {
        if (ElementSchemaType != null)
            yield return ElementSchemaType;
        
        if (Relations != null)
        {
            foreach (StructRelationSchema relation in Relations)
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
        return schema?.ToSchema().With(new ArraySchema
        {
            Element = schema.Element,
            Primary = schema.Primary,
            Indexes = schema.Indexes,
            Combines = schema.Combines,
            Relations = schema.Relations,
            Atomic = schema.Atomic,
        });
    }
    
    #endregion
    
    #region Generic type
    
    /// <summary>
    /// Gets the generic type of the array
    /// </summary>
    public async Task<ArrayType?> GetGenericTypeAsync(SchemaContext context, string elementType)
    {
        if (ElementSchemaType is not GenericType) return null;
        _genericArrayTypes ??= new ConcurrentDictionary<string, ArrayType>();
        
        NodeType? eleType = await context.GetNodeTypeAsync(elementType);
        if (eleType is null or GenericType or ArrayType) return null;
        
        return _genericArrayTypes.GetOrAdd(elementType, _ =>
        {
            ArrayType arrayType = new()
            {
                Name = $"{Name}<{elementType}>",
                Display = $"{Locale.LIST_PREFIX}{{@{elementType}}}{Locale.LIST_SUFFIX}",
                Namespace = Namespace,
                Element = elementType,
                ElementSchemaType = eleType,
                Atomic = Atomic,
                Loaded = true,
                LoadState = LoadState,
                Provider = Provider,
                Extensions = Extensions
            };
            eleType.AddUsedBy(arrayType);
            return arrayType;
        });
    }

    /// <summary>
    /// Gets the generic type of the array
    /// </summary>
    public ArrayType? GetGenericType(NodeType elementType)
    {
        if (ElementSchemaType is not GenericType) return null;
        _genericArrayTypes ??= new ConcurrentDictionary<string, ArrayType>();
        
        if (elementType is null or GenericType or ArrayType) return null;
        
        return _genericArrayTypes.GetOrAdd(elementType.Name.ToLower(), _ =>
        {
            ArrayType arrayType = new()
            {
                Name = $"{Name}<{elementType}>",
                Display = $"{Locale.LIST_PREFIX}{{@{elementType}}}{Locale.LIST_SUFFIX}",
                Namespace = Namespace,
                Element = elementType.Name,
                ElementSchemaType = elementType,
                Atomic = Atomic,
                Loaded = true,
                LoadState = LoadState,
                Provider = Provider,
                Extensions = Extensions
            };
            elementType.AddUsedBy(arrayType);
            return arrayType;
        });
    }
    
    private ConcurrentDictionary<string, ArrayType>? _genericArrayTypes;

    #endregion
}
