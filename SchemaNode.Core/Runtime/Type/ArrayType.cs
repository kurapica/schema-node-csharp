using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Collections.Immutable;
using SchemaNode.Property.Constraint;
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
    /// The indexes
    /// </summary>
    public ImmutableList<DataIndex>? Indexes { get; private set; }

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
        Element = !string.IsNullOrWhiteSpace(array.Element) ? await context.GetNodeTypeAsync<ValueType>(array.Element, Generics) : null;
        Primary = GetProperty<Primary>()?.Value?.ToImmutableList();
        Indexes = GetProperty<Indexes>()?.Value?.ToImmutableList();

        if (Element is GenericType && GenericParams is { Count: 1 })
            Element = GenericParams[0] as ValueType;

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
    public override void Release() => _relations = null;

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
    public override ValueType? GetSourceValueType(ReadOnlySpan<char> path)
    {
        if (path.IsEmpty || path.SequenceEqual(ARRAY_ITSELF)) return this;
        return path.SequenceEqual(ARRAY_ELEMENT) ? Element : null;
    }

    /// <inheritdoc />
    public override bool IsAssignableTo(ValueType other)
    {
        if (base.IsAssignableTo(other)) return true;
        if (other is not ArrayType array) return false;
        return Element == array.Element || Element != null && array.Element != null && Element.IsAssignableTo(array.Element);
    }

    /// <inheritdoc />
    public override DataNode ParseValue(object? value)
        => value is ArrayNode node && node.NodeType == this ? node : new ArrayNode(this, value);

    /// <inheritdoc />
    protected override async Task ValidateValueAsync(SchemaContext context, DataNode value)
    {
        if (Element == null || value is not ArrayNode result || result.NodeType == this)
        {
            value.ViolatedConstraints = [Kind];
            return;
        }

        // Validate by elements
        foreach (DataNode element in result)
            await Element.ValidateValueAsync(context, element);

        // Validate by relations
        if (_relations != null)
        {
            bool changed = false;
            foreach ((IRelationProcess process, Type propType) in _relations)
            {
                DataNode? propValue = await process.ProcessAsync(context, result);
                if (propValue == null) continue;
                
                // build the constraint property
                if (Activator.CreateInstance(propType) is not IConstraintProperty prop) continue;
                prop.SetValue(propValue);
                
                // apply constraint on target
                SpanReader spans = process.Target;
                List<DataNode> currNodes = [result];
                while (spans.NextPath())
                {
                    if (spans.IsEnd)
                    {
                        foreach (DataNode currNode in currNodes)
                        {
                            if (await prop.ValidateAsync(context, currNode) == false)
                            {
                                if (currNode.ViolatedConstraints != null &&
                                    currNode.ViolatedConstraints.Contains(prop.Name)) continue;
                                currNode.ViolatedConstraints = currNode.ViolatedConstraints is { Length: > 0 }
                                    ? currNode.ViolatedConstraints.Append(prop.Name).ToArray()
                                    : [prop.Name];
                                changed = true;
                            }
                            else if (currNode.ViolatedConstraints != null && currNode.ViolatedConstraints.Contains(prop.Name))
                            {
                                currNode.ViolatedConstraints = currNode.ViolatedConstraints.Length == 1 
                                    ? null 
                                    : currNode.ViolatedConstraints.Where(c => c != prop.Name).ToArray();
                                changed = true;
                            }
                        }
                        break;
                    }
                    
                    // Gather effect nodes
                    ReadOnlySpan<char> path = spans.Current;
                    List<DataNode> nextLevels = [];
                    foreach (DataNode currNode in currNodes)
                    {
                        if (currNode is ArrayNode arr)
                        {
                            foreach (DataNode element in arr)
                            {
                                DataNode? next = element.GetSourceValue(path);
                                if (next != null) nextLevels.Add(next);
                            }
                        }
                        else
                        {
                            DataNode? next = currNode.GetSourceValue(path);
                            if (next != null) nextLevels.Add(next);
                        }
                    }
                    currNodes = nextLevels;
                }
            }
            
            if (changed)
                foreach (DataNode field in result)
                    field.RefreshViolatedConstraints();
        }
        
        // Check
        if (result.Any(e => e.ViolatedConstraints is { Length: > 0 }))
            result.ViolatedConstraints = [Kind];
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

    #endregion
}
