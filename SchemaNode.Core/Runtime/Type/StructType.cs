using System.Reflection;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Schema;
using SchemaNode.Utility;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Struct;
using static SchemaNode.Utility.Constant;
using Type = System.Type;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory struct schema representation
/// </summary>
public sealed class StructType: ValueType
{
    #region Fields
    
    /// <summary>
    /// The struct fields
    /// </summary>
    private List<StructFieldType> _fields = [];

    /// <summary>
    /// The union validations
    /// </summary>
    private List<StructUnionValidation>? _unionValids;
    
    /// <summary>
    /// The relations between the fields
    /// </summary>
    private List<RelationType>? _relations;
    
    #endregion
        
    #region Implementations

    /// <inheritdoc />
    public override Type GetCsharpType() => base.GetCsharpType() ?? typeof(StructNode);

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
        if (@struct.GetProperty<Relations>()?.Value is { Length: > 0 } relations)
        {
            foreach (RelationSchema relation in relations)
            {
                // Gets the target type
                ValueType? currentType = GetAccessValueType(relation.Target);
                if (currentType == null) continue;
                
                // Only work for constraint properties
                Type? propType = context.Runtime.GetSchemaKindPropertyByName(currentType.Kind, relation.Property);
                if (propType == null || !typeof(IConstraintProperty).IsAssignableFrom(propType)) continue;
                
                var relationType = await relation.LoadAsync(context, this);
                Error ??= relationType.Error;

                _relations ??= [];
                _relations.Add(relationType);
            }
        }
        
        // Load Union Validation
        if (@struct.UnionValids is { Length: > 0 })
        {
            _unionValids = @struct.UnionValids
                .Where(v => !string.IsNullOrWhiteSpace(v.Func))
                .Select(v => new StructUnionValidation
                {
                    Func = v.Func,
                    Args = v.Args,
                }).ToList();
            foreach (StructUnionValidation valid in _unionValids)
            {
                FunctionType? funcNode = await context.GetNodeTypeAsync<FunctionType>(valid.Func);
                if (funcNode != null)
                {
                    valid.FuncNode = funcNode;
                }
                else
                {
                    valid.Error = ErrorCodes.STRUCT_VALID_FUNC_NOT_EXIST;
                    Error ??= valid.Error;
                }
            }
        }
    }

    /// <inheritdoc />
    public override void Release()
    {
        _fields = [];
        _relations = null;
        _unionValids = null;
    }

    /// <inheritdoc />
    public override IEnumerable<NodeType> GetReferenceTypes()
    {
        foreach (NodeType node in _fields.SelectMany(f => f.GetReferenceTypes()))
            yield return node;

        if (_relations != null)
            foreach (NodeType node in _relations.OfType<INodeReferences>().SelectMany(n => n.GetReferenceTypes()))
                yield return node;

        if (_unionValids != null)
            foreach(StructUnionValidation valid in _unionValids)
                if (valid.FuncNode != null)
                    yield return valid.FuncNode;
        
        foreach (NodeType nodeType in base.GetReferenceTypes())
            yield return nodeType;
    }

    /// <inheritdoc />
    public override ValueType? GetAccessValueType(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.SequenceEqual(NODE_SELF)) return this;
        
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
    public override bool IsAssignableTo(ValueType other)
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
    public override DataNode Create() => new StructNode(this);

    /// <inheritdoc />
    protected override async Task ValidateNodeAsync(SchemaContext context, DataNode value)
    {
        if (value is not StructNode result) return;
        
        // Validate by fields
        foreach (StructFieldType field in _fields.Where(f => f.Type != null && f.DisplayOnly != true))
        {
            DataNode? dataNode = result.GetAccessValue(field.Name);
            if (dataNode == null) continue;
            
            // Validate the struct fields
            await field.Type!.ValidateValueAsync(context, dataNode);

            if (field.Constraints is not { Length: > 0 }) continue;
            
            List<IProperty>? errors = null;
            List<IProperty>? passed = null;
            foreach (IConstraintProperty constraint in field.Constraints)
            {
                if (await constraint.ValidateAsync(context, dataNode) != false)
                {
                    passed ??= [];
                    passed.Add(constraint);
                }
                else
                {
                    errors ??= [];
                    errors.Add(constraint);
                }
            }
            if (errors != null || passed != null)
                dataNode.SetViolated(errors, passed);
        }

        // Validate by relations
        if (_relations != null)
        {
            foreach (RelationType process in _relations)
            {
                if (await process.ProcessAsync(context, result) is not IConstraintProperty prop) continue;
                
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
                        if (currNode is ArrayNode arr)
                        {
                            foreach (DataNode element in arr)
                            {
                                DataNode? next = element.GetAccessValue(path);
                                if (next != null) nextLevels.Add(next);
                            }
                        }
                        else
                        {
                            DataNode? next = currNode.GetAccessValue(path);
                            if (next != null) nextLevels.Add(next);
                        }
                    }
                    currNodes = nextLevels;
                }
            }
        }

        // Union validation
        if (_unionValids is { Count: > 0 })
        {
            foreach (StructUnionValidation valid in _unionValids.Where(v => v.Error == null))
            {
                var args = new object?[valid.Args.Length];
                DataNode? first = null;
                for(int i = 0; i < valid.Args.Length; i++)
                {
                    var arg = valid.Args[i];
                    if (!string.IsNullOrWhiteSpace(arg.Source))
                    {
                        DataNode? node = result.GetAccessValue(arg.Source);
                        first ??= node;
                        args[i] = node;
                    }
                    else
                    {
                        args[i] = arg.Value;
                    }
                }

                if (first == null) continue;
                try
                {
                    if (await valid.FuncNode!.CallAsync<bool>(context, args)) continue;
                }
                catch
                {
                    // ignore
                }
                first.SetViolated(valid.Func);
            }
        }
    }

    public override IEnumerable<Entry<string>> GetSubEntries()
    {
        return GetFields().Select(field => new Entry<string>
        {
            Value = field.Name,
            Label = field.GetProperty<Display>()?.Value,
            HasChildren = field.Type?.HasSubEntries  ?? false
        });
    }

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
    /// Gets or calc the field value
    /// </summary>
    public async Task<DataNode?> GetFieldValueAsync(SchemaContext context, StructNode node, string fieldName)
    {
        string[] paths = fieldName.Split('.', 2);
        StructFieldType? fieldType = GetField(paths[0]);
        if (fieldType == null) return null;
        
        DataNode? value = node.GetAccessValue(paths[0]);
        if (value == null) return null;
        if (!value.IsEmpty || fieldType.DisplayOnly != true) return paths.Length > 1 ? value.GetAccessValue(paths[1]) : null;
        
        // check relations
        RelationType? r = _relations?.FirstOrDefault(rel => rel.Target.Equals(fieldName, StringComparison.OrdinalIgnoreCase) && rel.ForProperty<Default>() );
        if (r == null) return value;
        
        // process relations
        IProperty? def = await r.ProcessAsync(context, node);
        value.TrySetValue(def?.GetValue<object>());
        return paths.Length > 1 ? value.GetAccessValue(paths[1]) : null;
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
    /// Gets relations
    /// </summary>
    public IEnumerable<RelationType> GetRelations() => _relations?.AsEnumerable() ?? [];
    
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
public class StructFieldType : INodeReferences
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
    /// The constraint properties from Extensions
    /// </summary>
    internal IConstraintProperty[]? Constraints { get; private set; }

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
    public DataNode? Default { get; private set; }

    /// <summary>
    /// The low limit of the scalar value.
    /// </summary>
    public object? LowLimit { get; private set; }

    /// <summary>
    /// The up limit of the scalar value.
    /// </summary>
    public object? UpLimit { get; private set; }

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

        // Properties
        IProperty[] props = field.GetProperties(context.Runtime.GetSchemaKindProperties(SCHEMA_KIND_STRUCT_FIELD)).ToArray();
        IConstraintProperty[] constraints = props.OfType<IConstraintProperty>().ToArray();
        
        (RefTypes, string? error) = await field.LoadPropertiesAsync(context, props, Type);
        field.Error ??= error;
        
        // init
        Name = field.Name;
        Properties = props;
        Constraints = constraints;

        // Useful properties
        Require = GetProperty<Require>()?.Value;
        DisplayOnly = GetProperty<DisplayOnly>()?.Value;
        Unpack =  GetProperty<Unpack>()?.Value;
        Default = GetProperty<Default>() is {} defProp ? await Type.ValidateValueAsync(context, defProp.Value) : null;
        UpLimit = GetProperty(nameof(UpLimit))?.GetValue<object>();
        LowLimit = GetProperty(nameof(LowLimit))?.GetValue<object>();
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
    /// Gets the property
    /// </summary>
    public IProperty? GetProperty(string propertyName) => Properties?.FirstOrDefault(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
    
    /// <summary>
    /// Get the property with property type
    /// </summary>
    public T? GetProperty<T>() where T : class, IProperty => Properties?.OfType<T>().FirstOrDefault();
}