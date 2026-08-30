using SchemaNode.Context;
using SchemaNode.Node;
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
public sealed class ArrayType: ValueType, IRelationProvider
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
                
                // Gets the property type
                PropertyType? prop = await context.GetNodeTypeAsync<PropertyType>(relation.Property);
                if (prop == null) continue;
                
                // Only work for constraint properties
                Type? propType = prop.GetCsharpType();
                if (propType == null) continue;
                
                RelationType relationType = await relation.LoadAsync(context, this);
                Error ??= relationType.Error;
                
                _relations ??= [];
                _relations.Add(relationType);
            }
        }
    }

    /// <inheritdoc />
    public override void Unload() => _relations = null;

    /// <inheritdoc />
    public override IEnumerable<NodeType> GetReferenceTypes()
    {
        if (Element != null)
            yield return Element;
        
        if (_relations != null)
            foreach (NodeType node in _relations.OfType<INodeReferences>().SelectMany(n => n.GetReferenceTypes()))
                yield return node;
        
        foreach (NodeType nodeType in base.GetReferenceTypes())
            yield return nodeType;
    }

    /// <inheritdoc />
    public override ValueType? GetAccessValueType(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Equals(NODE_SELF, StringComparison.OrdinalIgnoreCase) || path.Equals(ARRAY_PREVIOUS, StringComparison.OrdinalIgnoreCase)) return this;
        string[] paths = path.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        if (paths[0] != ARRAY_ELEMENT) return null;
        return paths.Length <= 1 ? Element : Element?.GetAccessValueType(paths[1]);
    }

    /// <inheritdoc />
    public override IEnumerable<Entry<string>> GetAccessEntries()
    {
        // previous
        yield return new Entry<string> { Value = ARRAY_PREVIOUS };

        if (Element == null) yield break;
        
        // element
        yield return new Entry<string> { Value = ARRAY_ELEMENT, HasChildren = Element?.HasAccessEntries };
    }

    /// <inheritdoc />
    public override bool HasAccessEntries => Element?.HasAccessEntries ?? false;

    /// <inheritdoc />
    public override bool IsAssignableTo(ValueType other)
    {
        if (base.IsAssignableTo(other)) return true;
        if (other is not ArrayType array) return false;
        return Element == array.Element || Element != null && array.Element != null && Element.IsAssignableTo(array.Element);
    }

    /// <inheritdoc />
    public override DataNode Create(IValueAccess? parent = null, IPropertyProvider? propertyProvider = null) => new ArrayNode(this, parent, propertyProvider);

    /// <inheritdoc />
    public IEnumerable<RelationType> GetRelations() => _relations ?? [];

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
    
    #region Property
    
    /// <summary>
    /// Gets the property with the given type
    /// </summary>
    public override T? GetProperty<T>() where T : class 
        => base.GetProperty<T>() ?? Element?.GetProperty<T>() ?? Runtime?.GetSchemaKindProperty<T>(Kind);

    /// <summary>
    /// Gets the properties with the given type
    /// </summary>
    public override IEnumerable<T> GetProperties<T>()
        => this.JoinProperties(base.GetProperties<T>(), Element?.GetProperties<T>(), Runtime?.GetSchemaKindProperties<T>(Kind));
    
    #endregion
}
