using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Collections.Immutable;
using SchemaNode.Property.Constraint;
using SchemaNode.Struct;
using static SchemaNode.Utility.Constant;
using Type = System.Type;

// ReSharper disable UnusedAutoPropertyAccessor.Global

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
    public ImmutableList<string>? Primary { get; private set; }

    /// <summary>
    /// The relations between the fields
    /// </summary>
    private List<RelationType>? _relations;

    #endregion

    #region Implementation

    /// <inheritdoc />
    public override Type GetCsharpType() => Element?.GetCsharpType() is { } type && !type.IsAssignableTo(typeof(DataNode)) ? typeof(List<>).MakeGenericType(type) : typeof(ArrayNode);

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context)
    {
        ArraySchema? array = GetProperty<ArrayProperty>()?.Value;
        if (array == null)
        {
            Error = ErrorCodes.NO_DEFINITION;
            return;
        }
        
        // load properties
        Element = !string.IsNullOrWhiteSpace(array.Element) ? await context.GetNodeTypeAsync<ValueType>(array.Element, Generics, GenericParams) : null;
        Primary = GetProperty<Primary>()?.Value?.ToImmutableList();

        if (Element == null)
        {
            Error = ErrorCodes.ARRAY_WRONG_ELEMENT;
            return;
        }
        
        // Load Relation
        if (GetProperty<Relations>()?.Value is { Length: > 0 } relations)
        {
            foreach (RelationSchema relation in relations)
            {
                // Gets the target type
                ValueType? currentType = GetAccessValueType(relation.Target);
                if (currentType == null) continue;
                
                // Only check constraint properties
                Type? propType = context.Runtime.GetSchemaKindPropertyByName(currentType.Kind, relation.Property);
                if (propType == null || !typeof(IConstraintProperty).IsAssignableFrom(propType)) continue;
                
                RelationType relationType = await relation.LoadAsync(context, this);
                Error ??= relationType.Error;
                
                _relations ??= [];
                _relations.Add(relationType);
            }
        }
    }

    /// <inheritdoc />
    public override void Release() => _relations = null;

    /// <inheritdoc />
    public override IEnumerable<NodeType> GetReferenceTypes()
    {
        if (Element != null)
            yield return Element;
        
        if (_relations != null)
            foreach (NodeType node in _relations.Cast<INodeReferences>().SelectMany(n => n.GetReferenceTypes()))
                yield return node;
        
        foreach (NodeType nodeType in base.GetReferenceTypes())
            yield return nodeType;
    }

    /// <inheritdoc />
    public override ValueType? GetAccessValueType(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.SequenceEqual(NODE_SELF) || path.SequenceEqual(ARRAY_PREVIOUS)) return this;
        return path.SequenceEqual(ARRAY_ELEMENT) ? Element : Element?.GetAccessValueType(path);
    }

    /// <inheritdoc />
    public override IEnumerable<Entry<string>> GetSubEntries()
    {
        // previous
        yield return new Entry<string>
        {
            Value = ARRAY_PREVIOUS
        };

        if (Element == null) yield break;
        
        // element
        yield return new Entry<string>
        {
            Value = ARRAY_ELEMENT,
        };

        foreach (Entry<string> entry in Element.GetSubEntries())
            yield return entry;
    }

    /// <inheritdoc />
    public override bool HasSubEntries => Element?.HasSubEntries ?? false;

    /// <inheritdoc />
    public override bool IsAssignableTo(ValueType other)
    {
        if (base.IsAssignableTo(other)) return true;
        if (other is not ArrayType array) return false;
        return Element == array.Element || Element != null && array.Element != null && Element.IsAssignableTo(array.Element);
    }

    /// <inheritdoc />
    public override DataNode Create() => new ArrayNode(this);

    /// <inheritdoc />
    protected override async Task ValidateNodeAsync(SchemaContext context, DataNode value)
    {
        if (Element == null || value is not ArrayNode result || result.Type != this) return;

        // Validate by elements
        foreach (DataNode element in result)
            await Element.ValidateValueAsync(context, element);

        // Validate by relations
        if (_relations != null)
        {
            foreach (RelationType relationType in _relations)
            {
                for (int i = 1; i < result.Count; i++)
                {
                    ArrayNode spanNode = new ArrayNode(result, i);
                    IConstraintProperty? prop = await relationType.ProcessAsync(context, spanNode) as IConstraintProperty;
                    if (prop is not { HasValue: true }) continue;

                    // apply constraint on target
                    SpanReader spans = relationType.Target;
                    List<DataNode> currNodes = [spanNode];
                    while (spans.NextPath())
                    {
                        if (spans.IsEnd)
                        {
                            foreach (DataNode currNode in currNodes)
                            {
                                if (await prop.ValidateAsync(context, currNode) == false)
                                {
                                    if (currNode.Violated != null && currNode.Violated.Contains(prop.Name)) continue;
                                    currNode.SetViolated(prop);
                                }
                                else if (currNode.Violated != null && currNode.Violated.Contains(prop.Name))
                                {
                                    currNode.ClearViolated(prop);
                                }
                            }
                            break;
                        }

                        // Gather effect nodes
                        ReadOnlySpan<char> path = spans.Current;
                        List<DataNode> nextLevels = [];
                        foreach (DataNode currNode in currNodes)
                        {
                            DataNode? next = currNode.GetAccessValue(path);
                            if (next != null) nextLevels.Add(next);
                        }
                        currNodes = nextLevels;
                    }
                }
            }
        }
    }

    #endregion

    #region Method

    /// <summary>
    /// Get unique key for object
    /// </summary>
    public string[]? GetPrimaryKeys<T>(IDictionary<string, T> obj)
    {
        if (Primary == null || Primary.Count == 0 || Element is not StructType @struct)
            return null;

        string[] keys = new string[Primary.Count];
        for (var index = 0; index < Primary.Count; index++)
        {
            var p = Primary[index];
            StructFieldType? fld = @struct.GetField(p);
            if (fld == null) return null;

            if (obj.TryGetValue(p, out T? objValue) && objValue?.ToString() is { } str && !string.IsNullOrWhiteSpace(str))
            {
                keys[index] = str;
            }
            else
            {
                return null;
            }
        }
        return keys;
    }
    
    public string[]? GetPrimaryKeys(IValueAccess obj)
    {
        if (Primary == null || Primary.Count == 0 || Element is not StructType @struct)
            return null;

        string[] keys = new string[Primary.Count];
        for (var index = 0; index < Primary.Count; index++)
        {
            var p = Primary[index];
            StructFieldType? fld = @struct.GetField(p);
            if (fld == null) return null;

            if (obj.GetAccessValue(p) is {} objValue && objValue.ToString() is { } str && !string.IsNullOrWhiteSpace(str))
            {
                keys[index] = str;
            }
            else
            {
                return null;
            }
        }
        return keys;
    }

    /// <summary>
    /// Gets the unique key for the object with separator, returns null if any of the primary keys is missing or empty
    /// </summary>
    public string? GetPrimaryKey<T>(IDictionary<string, T> obj, string sep = "|")
    {
        if (Primary == null || Primary.Count == 0 || Element is not StructType @struct)
            return null;

        string[] keys = new string[Primary.Count];
        for (var index = 0; index < Primary.Count; index++)
        {
            var p = Primary[index];
            StructFieldType? fld = @struct.GetField(p);
            if (fld == null) return null;

            if (obj.TryGetValue(p, out T? objValue) && objValue?.ToString() is { } str && !string.IsNullOrWhiteSpace(str))
            {
                keys[index] = str;
            }
            else
            {
                return null;
            }
        }
        return string.Join(sep, keys);
    }
    
    public string? GetPrimaryKey(IValueAccess obj, string sep = "|")
    {
        if (Primary == null || Primary.Count == 0 || Element is not StructType @struct)
            return null;

        string[] keys = new string[Primary.Count];
        for (var index = 0; index < Primary.Count; index++)
        {
            var p = Primary[index];
            StructFieldType? fld = @struct.GetField(p);
            if (fld == null) return null;

            if (obj.GetAccessValue(p) is {} objValue && objValue.ToString() is { } str && !string.IsNullOrWhiteSpace(str))
            {
                keys[index] = str;
            }
            else
            {
                return null;
            }
        }
        return string.Join(sep, keys);
    }

    #endregion
}
