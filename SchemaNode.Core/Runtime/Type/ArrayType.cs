using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.Json.Nodes;
using static SchemaNode.Utility.Constant;
using JsonNode = System.Text.Json.Nodes.JsonNode;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory array schema representation
/// </summary>
public sealed class ArrayType: ValueType
{
    #region Fields

    /// <summary>
    /// The element type node
    /// </summary>
    public ValueType? Element { get; private set; }

    /// <summary>
    /// The primary fields of the array if the element is a struct.
    /// </summary>
    public ImmutableArray<string>? Primary { get; private set; }

    /// <summary>
    /// The indexes
    /// </summary>
    public ImmutableArray<DataIndex>? Indexes { get; private set; }

    /// <summary>
    /// The data combine rule
    /// </summary>
    public ImmutableArray<DataCombine>? Combines { get; private set; }

    /// <summary>
    /// The relations between the fields
    /// </summary>
    private List<(IRelationProcess, Type)>? _relations;

    #endregion
    
    #region Method

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context)
    {
        ArraySchema? array = GetPropertyValue<ArraySchema>();
        if (array == null)
        {
            Error = ErrorCodes.NO_DEFINITION;
            return;
        }
        
        // load properties
        Element = !string.IsNullOrWhiteSpace(array.Element) ? await context.GetNodeTypeAsync<ValueType>(array.Element) : null;
        Primary = array?.Primary?.ToImmutableArray();
        Combines = array?.Combines?.ToImmutableArray();
        Indexes = array?.Indexes?.ToImmutableArray();

        if (Element == null)
        {
            Error = ErrorCodes.ARRAY_WRONG_ELEMENT;
            return;
        }
        
        // Load Relation
        if (array!.GetProperty<Relations>()?.Value is { Length: > 0 } relations)
        {
            foreach (RelationSchema relation in relations)
            {
                // Gets the target type
                SpanReader paths = relation.Target;
                ValueType? currentType = this;
                while (currentType != null && paths.NextPath())
                {
                    currentType = currentType switch
                    {
                        StructType s => s.GetField(paths.Current)?.Type,
                        ArrayType { Element: StructType s } => s.GetField(paths.Current)?.Type,
                        _ => null
                    };
                }
                if (currentType == null) continue;
                
                // Only check constraint properties
                Type? propType = context.Runtime.GetSchemaKindPropertyByName(currentType.Kind, relation.Property);
                if (propType == null || !typeof(IConstraintProperty).IsAssignableFrom(propType)) continue;
                
                IRelationProcess? process = await context.GetRelationProcessAsync(this, relation);
                switch (process)
                {
                    case null:
                        continue;
                    case INodeError error when !string.IsNullOrWhiteSpace(error.Error):
                        Error ??= error.Error;
                        break;
                }

                _relations ??= [];
                _relations.Add((process, propType));
            }
        }
    }

    /// <inheritdoc />
    public override void Release()
    {
        _relations = null;
    }

    /// <inheritdoc />
    public override IEnumerable<NodeType> GetReferenceTypes()
    {
        if (Element != null)
            yield return Element;
        
        if (_relations != null)
            foreach (NodeType node in _relations.Select(r => r.Item1).Cast<INodeReferences>().SelectMany(n => n.GetReferenceTypes()))
                yield return node;
        
        foreach (NodeType nodeType in base.GetReferenceTypes())
            yield return nodeType;
    }

    /// <inheritdoc />
    public override bool IsAssignableTo(ValueType other)
    {
        if (base.IsAssignableTo(other)) return true;
        if (other is not ArrayType array) return false;
        if (Element == null || array.Element == null) return true;
        return Element.IsAssignableTo(array.Element);
    }

    /// <inheritdoc />
    public override async Task<Node.DataNode?> ValidateValueAsync(SchemaContext context, JsonNode value, IReadOnlyList<IConstraintProperty>? constraints = null)
    {
        if (value is not JsonArray array)
            return (null, TYPE_VALUE_NOT_VALID);

        // validate elements
        ArrayNode result = new(this);
        JsonObject? error = null;
        if (Element != null)
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
                (Node.DataNode? v, JsonNode? e) = await Element.ValidateValueAsync(context, array[i]!, eleConstraints);
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
        || (other is ArrayType array && Element != null && array.Element != null && Element.CanBeUseAs(array.Element));

    /// <inheritdoc />
    public override ArrayType? GetArrayType(bool exactly = false) => null;

    /// <summary>
    /// Get unique key for object
    /// </summary>
    public string? GetPrimaryKey(JsonObject obj)
    {
        if (Primary == null || Primary.Length == 0 || Element is not StructType { Fields.Length: > 0 } @struct)
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
        if (Primary == null || Primary.Length == 0 || Element is not StructType { Fields.Length: > 0 } @struct)
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
        if (Element != null)
            yield return Element;
        
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
        if (Element is not GenericType) return null;
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
                Element = eleType,
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
        if (Element is not GenericType) return null;
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
                Element = elementType,
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
