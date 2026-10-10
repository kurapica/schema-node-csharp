using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Struct;
using SchemaNode.Runtime.Interface;
using SchemaNode.Schema;
using SchemaNode.Struct;
using SchemaNode.Utility;
using System.Reflection;
using System.Xml.Linq;
using static SchemaNode.Utility.Constant;
using Type = System.Type;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory struct schema representation
/// </summary>
public sealed class StructType: ValueType, IRelationProvider, INodeTypeReLoadHandler
{
    #region Fields
    
    /// <summary>
    /// The struct fields
    /// </summary>
    private List<StructFieldType> _fields = [];
        
    /// <summary>
    /// The relations between the fields
    /// </summary>
    private List<RelationType>? _relations;

    #endregion

    #region Implementations

    /// <inheritdoc />
    public void OnNodeTypeLoaded(NodeType[] types)
    {
        if (types.Any(t => _fields.Any(f => f.Type == t)))
            this.NotifyReload(types);
    }

    /// <inheritdoc />
    public override Type GetCsharpType(bool nullable = false) => base.GetCsharpType() ?? typeof(StructNode);

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context)
    {
        // reset
        _fields = [];
        _relations = null;
        
        // load struct schema
        StructSchema? @struct = GetProperty<StructProperty>()?.Value;
        if (@struct == null)
        {
            Error = ErrorCodes.NO_DEFINITION;
            return;
        }
        
        // load fields
        Type? cType = Schema?.Type;
        foreach (StructFieldSchema field in @struct.Fields)
        {
            field.Error = null;
            StructFieldType fieldType = new();
            await fieldType.LoadAsync(context, field, Generics, GenericParams, cType?.GetProperty(field.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase));
            Error ??= field.Error;
            _fields.Add(fieldType);
        }

        // Load Relation
        if (IsGeneric) return;
        if (@struct.GetProperty<Relations>()?.Value is { Length: > 0 } relations)
        {
            foreach (RelationSchema relation in relations)
            {
                // Gets the target type
                IValueTypeAccess? currentType = GetAccessValueType(relation.Target);
                if (currentType == null) continue;
                
                // Gets the property type
                var kind = (context.Runtime as SchemaRuntime)?.GetSchemaKindByNodeKind(currentType.Kind);
                PropertyType? prop = await context.GetPropertyType(relation.Property, kind, SCHEMA_KIND_NODE_STRUCT_FIELD);
                if (prop == null) continue;
                
                // Only work for constraint properties
                Type? propType = prop.GetCsharpType();
                if (propType == null) continue;
                
                var relationType = await relation.LoadAsync(context, this, kind, SCHEMA_KIND_NODE_STRUCT_FIELD);
                Error ??= relationType.Error;

                _relations ??= [];
                _relations.Add(relationType);
            }
        }
    }

    /// <inheritdoc />
    public override void Unload()
    {
        _fields = [];
        _relations = null;
    }
    
    /// <summary>
    /// Gets the property with the given type
    /// </summary>
    public override T? GetProperty<T>() where T : class 
        => base.GetProperty<T>() ?? Runtime?.GetSchemaKindProperty<T>(SchemaKind);

    /// <summary>
    /// Gets the properties with the given type
    /// </summary>
    public override IEnumerable<T> GetProperties<T>()
        => this.JoinProperties(base.GetProperties<T>(), Runtime?.GetSchemaKindProperties<T>(SchemaKind));

    /// <inheritdoc />
    public override IEnumerable<NodeType> GetReferenceTypes()
    {
        foreach (StructFieldType node in _fields.ToArray())
        {
            foreach (NodeType nodeType in node.GetReferenceTypes())
                yield return nodeType;
        }

        if (_relations != null)
            foreach (var r in _relations.OfType<INodeReferences>())
                foreach (NodeType node in r.GetReferenceTypes())
                    yield return node;
                
        foreach (NodeType nodeType in base.GetReferenceTypes())
            yield return nodeType;
    }

    /// <inheritdoc />
    public override IValueTypeAccess? GetAccessValueType(string path)
    {
        IValueTypeAccess? g = base.GetAccessValueType(path);
        if (g != null) return g;
        
        ReadOnlySpan<char> remain = null;
        int index = path.IndexOf('.');
        if (index > 0)
        {
            remain = path.AsSpan()[(index + 1)..];
            path = path[..index];
        }
        foreach (StructFieldType field in _fields)
        {
            if (path.Equals(field.Name, StringComparison.OrdinalIgnoreCase))
                return remain.IsEmpty ? field.Type : field.Type?.GetAccessValueType(remain.ToString());
        }
        return null;
    }

    /// <inheritdoc />
    public override bool IsAssignableTo(IValueTypeAccess other)
    {
        if (base.IsAssignableTo(other)) return true;
        if (other is not StructType @struct) return false;
        return @struct._fields.Any(v => _fields.Any(f => f.Name.Equals(v.Name, StringComparison.OrdinalIgnoreCase))) 
               && @struct._fields.All(v =>
               {
                   StructFieldType? match = _fields.FirstOrDefault(f => f.Name.Equals(v.Name, StringComparison.OrdinalIgnoreCase));
                   return match?.Type == null ? !(v.Require ?? false) : v.Type != null && match.Type.IsAssignableTo(v.Type);
               });
    }

    /// <inheritdoc />
    public override IValueAccess Create(IValueAccess? parent = null, IPropertyProvider? propertyProvider = null) => new StructNode(this, parent, propertyProvider);

    /// <inheritdoc />
    public override IEnumerable<Entry<string>> GetAccessEntries()
    {
        return GetFields().Select(field =>
        {
            var entry = new Entry<string>
            {
                Value = field.Name,
                HasChildren = field.Type?.HasAccessEntries ?? false
            };
            var display = field.GetProperty<Display>();
            if (display != null) entry.SetProperty(display);
            return entry;
        });
    }

    /// <summary>
    /// Gets relations
    /// </summary>
    public override IEnumerable<RelationType> GetRelations() => _relations?.AsEnumerable() ?? [];

    #endregion

    #region Methods
    
    /// <summary>
    /// Gets the fields
    /// </summary>
    public IEnumerable<StructFieldType> GetFields() => _fields.AsEnumerable();

    /// <summary>
    /// Gets the field by name
    /// </summary>
    public StructFieldType? GetField(ReadOnlySpan<char> fieldName)
    {
        foreach (StructFieldType field in _fields)
        {
            if  (field.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                return field;
        }
        return null;
    }
        
    /// <summary>
    /// Gets the field index, -1 if not found
    /// </summary>
    public int GetIndex(ReadOnlySpan<char> fieldName)
    {
        for (int i = 0; i < _fields.Count; i++)
        {
            if (fieldName.SeqEquals(_fields[i].Name, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }
    
    /// <summary>
    /// Gets relations for the given field name
    /// </summary>
    public IEnumerable<RelationType> GetRelations(string fieldName)
        => _relations?.Where(r => fieldName.Equals(r.Target, StringComparison.OrdinalIgnoreCase)) ?? [];
    
    #endregion
}

/// <summary>
/// The runtime struct field type
/// </summary>
public class StructFieldType : INodeReferences, IPropertyProvider
{
    /// <summary>
    /// The field name
    /// </summary>
    public string Name { get; private set; } = String.Empty;

    /// <summary>
    /// The type node ref
    /// </summary>
    public ValueType? Type { get; private set; }
    
    /// <summary>
    /// The property info
    /// </summary>
    public PropertyInfo? Property { get; private set; }
    
    /// <summary>
    /// The properties
    /// </summary>
    internal IProperty[]? Properties { get; private set; }

    /// <summary>
    /// The constraint properties
    /// </summary>
    public IEnumerable<IConstraintProperty> Constraints => GetProperties<IConstraintProperty>().Reverse();

    /// <summary>
    /// The ref types from the properties in Extensions
    /// </summary>
    internal NodeType[]? RefTypes { get; private set; }
    
    /// <summary>
    /// The node data is required.
    /// </summary>
    public bool? Require { get; private set; }

    /// <summary>
    /// The node should be display only, won't be submitted.
    /// </summary>
    public bool? DisplayOnly { get; private set; }

    /// <summary>
    /// Unpack/pack additional data for the json node.
    /// </summary>
    public bool? Unpack { get; private set; }

    /// <summary>
    /// The default value of the node.
    /// </summary>
    public IValueAccess? Default { get; private set; }

    /// <summary>
    /// Load struct field schema
    /// </summary>
    internal async Task LoadAsync(SchemaContext context, StructFieldSchema field, IReadOnlyList<GenericParameter>? generics = null, IReadOnlyList<NodeType>? genericParams = null, PropertyInfo? property = null)
    {
        Property = property;
        Type = await context.GetNodeTypeAsync<ValueType>(field.Type, generics, genericParams);

        if (Type == null)
        {
            field.Error = ErrorCodes.STRUCT_FIELD_WRONG_TYPE;
            return;
        }

        ValueType? propType = Type;
        if (propType is ArrayType arrayType) propType = arrayType.Element;

        // Properties
        var propTypes = context.Runtime.GetSchemaKindPropertyTypes(SCHEMA_KIND_NODE_STRUCT_FIELD);
        if (propType != null) propTypes = propTypes.Concat(context.Runtime.GetSchemaKindPropertyTypes((context.Runtime as SchemaRuntime)!.GetSchemaKindByNodeKind(propType.Kind))).Distinct();
        IProperty[] props = field.GetProperties(propTypes).ToArray();
        
        (RefTypes, string? error) = await field.LoadPropertiesAsync(context, props, Type);
        field.Error ??= error;
        
        // init
        Name = field.Name;
        Properties = props;

        // Useful properties
        Require = GetProperty<Require>()?.Value;
        DisplayOnly = GetProperty<DisplayOnly>()?.Value;
        Unpack =  GetProperty<Unpack>()?.Value;
        Default = GetProperty<Default>() is {} defProp ? await Type.ValidateValueAsync(context, defProp.Value) : null;
    }

    /// <summary>
    /// Gets all references types
    /// </summary>
    public IEnumerable<NodeType> GetReferenceTypes()
    {
        if (Type != null) yield return Type;
        if (RefTypes != null)
            foreach (NodeType nodeType in RefTypes)
                yield return nodeType;
    }
    
    /// <summary>
    /// Get the property with property type
    /// </summary>
    public T? GetProperty<T>() where T : class, IProperty 
        => Properties?.OfType<T>().FirstOrDefault() ?? (Type?.GetProperty<T>() is { Inheritable: true } p ? p : null);

    /// <summary>
    /// Gets the properties
    /// </summary>
    public IEnumerable<T> GetProperties<T>() where T : IProperty
        => this.JoinProperties(Properties?.OfType<T>(), Type?.GetProperties<T>().Where(p => p.Inheritable));
}