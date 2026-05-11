using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Text.Json.Nodes;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Presentation;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;
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
    private List<IRelationProcess>? _relations;
    
    #endregion
        
    #region Implementations

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context)
    {
        // reset
        _fields = [];
        _relations = null;
        
        // load struct schema
        StructSchema? @struct = GetPropertyValue<StructSchema>();
        if (@struct == null)
        {
            Error = ErrorCodes.NO_DEFINITION;
            return;
        }
        
        // load fields
        foreach (StructFieldSchema field in @struct.Fields)
        {
            field.Error = null;
            StructFieldType fieldType = new();
            await fieldType.LoadAsync(context, field, Generics, GenericParams);
            Error ??= field.Error;
            _fields.Add(fieldType);
        }

        // Load Relation
        if (@struct.GetProperty<Relations>()?.Value is { Length: > 0 } relations)
        {
            foreach (RelationSchema relation in relations)
            {
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
                _relations.Add(process);
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

    /// <summary>
    /// Gets the references types
    /// </summary>
    public override IEnumerable<NodeType> GetReferenceTypes()
    {
        foreach (NodeType node in _fields.SelectMany(f => f.GetReferenceTypes()))
            yield return node;

        if (_relations != null)
            foreach (NodeType node in _relations.Cast<INodeReferences>().SelectMany(n => n.GetReferenceTypes()))
                yield return node;

        if (_unionValids != null)
            foreach(StructUnionValidation valid in _unionValids)
                if (valid.FuncNode != null)
                    yield return valid.FuncNode;
        
        foreach (NodeType nodeType in base.GetReferenceTypes())
            yield return nodeType;
    }

    /// <inheritdoc />
    public override ValueType? GetValueTypeByPath(PathReader reader)
    {
        if (!reader.TryRead(out ReadOnlySpan<char> current)) return this;
        foreach (StructFieldType field in _fields)
        {
            if (current.Equals(field.Name, StringComparison.OrdinalIgnoreCase))
                return field.Type?.GetValueTypeByPath(reader);
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
    public override DataNode ParseValue(object? value)
        => value is StructNode node && node.NodeType == this ? node : new StructNode(this, value);

    /// <inheritdoc />
    public override async Task<DataNode> ValidateValueAsync(SchemaContext context, object? value)
    {
        StructNode result = (ParseValue(value) as StructNode)!;
        bool hasError = false;
        
        // Validate by fields
        foreach (StructFieldType field in _fields.Where(f => f.Type != null && f.DisplayOnly != true))
        {
            DataNode? dataNode = result.GetField(field.Name);
            if (dataNode == null) continue;
            await field.Type!.ValidateValueAsync(context, dataNode);

            if (field.Constraints != null)
            {
                HashSet<string>? errors = dataNode.ViolatedConstraints?.ToHashSet() ?? [];
                foreach (IConstraintProperty constraint in field.Constraints)
                {
                    if (await constraint.ValidateAsync(context, dataNode) != false)
                    {
                        errors?.Remove(constraint.Name);
                    }
                    else
                    {
                        errors ??= [];
                        errors.Add(constraint.Name);
                    }
                }
                dataNode.ViolatedConstraints = errors is { Count: > 0 } ? errors.ToArray() : null;
            }
        }
        hasError = result.Fields.Any(f => f.ViolatedConstraints is { Length: > 0 });

        // Union validation
        if (_unionValids is { Count: > 0 })
        {
            
        }

        // Validate by relations
        if (_relations != null)
        {
            
        }

        // error check
        if (hasError)
            result.ViolatedConstraints = [Kind];
        
        return result;
    }

    #endregion

    #region Methods
    
    /// <summary>
    /// Gets the field by name
    /// </summary>
    public StructFieldType? GetField(string fieldName) 
        => _fields.FirstOrDefault(f => f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
    
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
    internal async Task LoadAsync(SchemaContext context, StructFieldSchema field, GenericParameter[]? genericParams = null, IReadOnlyList<NodeType>? genericTypes = null)
    {
        if (await context.GetNodeTypeAsync(field.Type, genericParams) is not ValueType valueType)
        {
            field.Error = ErrorCodes.STRUCT_FIELD_WRONG_TYPE;
            return;
        }

        // Generic type resolution
        if (valueType is GenericType gen && genericTypes is { Count: > 0 })
        {
            int index = Array.FindIndex(genericParams ?? [], p => p.Name.Equals(gen.Name, StringComparison.OrdinalIgnoreCase));
            if (index >= 0 && index < genericTypes.Count)
                valueType = genericTypes[index] as ValueType ?? valueType;
            if (valueType is GenericType)
            {
                field.Error = ErrorCodes.STRUCT_FIELD_WRONG_TYPE;
                return;
            }
        }
        Type = valueType;
        
        // Properties
        IProperty[] props = context.Runtime.GetSchemaKindProperties(SCHEMA_KIND_STRUCT_FIELD)
            .Select(field.GetProperty).Where(p => p is { HasValue: true })
            .Cast<IProperty>().ToArray();
        IConstraintProperty[] constraints = props.Cast<IConstraintProperty>().ToArray();
        ITypeRefProperty[] typeRefs = props.Cast<ITypeRefProperty>().ToArray();
        
        // ref types
        List<NodeType>? refTypes = null;
        foreach (ITypeRefProperty prop in typeRefs)
        {
            string? name = prop.GetValue<string>();
            NodeType? refType = !string.IsNullOrWhiteSpace(name) ? await context.GetNodeTypeAsync(name) : null;
            if (refType != null)
            {
                refTypes ??= [];
                refTypes.Add(refType);
            }
            else
            {
                field.Error = ErrorCodes.WRONG_REF_TYPE;
                context.LogWarning($"Failed to load ref type '{name}' for property '{prop.Name}' in struct field '{field.Name}'");
            }
        }
        
        // init
        Name = field.Name;
        Type = valueType;
        Properties = props;
        Constraints = constraints;
        RefTypes = refTypes?.ToArray();

        // Useful properties
        Require = props.FirstOrDefault(p => p is Require) is Require r ? r.Value : null;
        DisplayOnly = props.FirstOrDefault(p => p is DisplayOnly) is DisplayOnly d ? d.Value : null;
        Unpack = props.FirstOrDefault(p => p is Unpack) is Unpack u ? u.Value : null;
        Default = props.FirstOrDefault(p => p is Default) is Default defProp ? await valueType.ValidateValueAsync(context, defProp.Value) : null;
        UpLimit = props.FirstOrDefault(p => p.Name.Equals(nameof(UpLimit), StringComparison.OrdinalIgnoreCase)) is IConstraintProperty up ? up.GetValue<object>() : null;
        LowLimit = props.FirstOrDefault(p => p.Name.Equals(nameof(LowLimit), StringComparison.OrdinalIgnoreCase)) is IConstraintProperty low ? low.GetValue<object>() : null;
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
}