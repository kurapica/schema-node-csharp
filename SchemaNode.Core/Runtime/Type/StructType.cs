using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Collections.Concurrent;
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
        GenericParameter[]? genericParams = @struct.GetProperty<Generics>()?.Value;
        
        // load fields
        foreach (StructFieldSchema field in @struct.Fields)
        {
            field.Error = null;
            StructFieldType fieldType = new();
            await fieldType.LoadAsync(context, field, genericParams, Generics);
            Error ??= field.Error;
            _fields.Add(fieldType);
        }

        // Load Relation
        RelationSchema[]? relations = @struct.GetProperty<RelationsProperty>()?.Value;
        if (relations is { Length: > 0 })
        {
            foreach (RelationSchema relation in relations)
            {
                IRelationProcess? process = await context.GetRelationProcessAsync(this, relation);
                if (process == null) continue;
                if (process is INodeError error && !string.IsNullOrWhiteSpace(error.Error))
                    Error ??= error.Error;
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
    public new IEnumerable<NodeType> GetReferenceTypes()
    {
        foreach (StructFieldType field in _fields)
        {
            foreach (NodeType node in field.GetReferenceTypes())
                yield return node;
        }

        if (_relations != null)
        {
            foreach (INodeReferences nodeRefs in _relations.Cast<INodeReferences>())
            foreach (NodeType node in nodeRefs.GetReferenceTypes())
                yield return node;
        }

        if (_unionValids != null)
        {
            foreach(StructUnionValidation valid in _unionValids)
                if (valid.FuncNode != null)
                    yield return valid.FuncNode;
        }
        
        foreach (NodeType nodeType in base.GetReferenceTypes())
            yield return nodeType;
    }

    /// <inheritdoc />
    public override ValueType? GetChildValueType(string path)
    {
        string[] parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        StructFieldType? field = _fields.FirstOrDefault(f => f.Name.Equals(parts[0], StringComparison.OrdinalIgnoreCase));
        if (field == null) return null;
        return parts.Length == 1 ? field.Type : field.Type?.GetChildValueType(string.Join('.', parts.Skip(1)));
    }

    /// <inheritdoc />
    public override async Task<(Node.DataNode? value, JsonNode? error)> ValidateValueAsync(SchemaContext context, JsonNode value, IReadOnlyList<IConstraintProperty>? constraints = null)
    {
        if (value is not JsonObject jObject)
            return (null, TYPE_VALUE_NOT_VALID);
        
        // validate fields
        StructNode result = new(this);
        JsonObject? error = null;
        string? additionalField = null;
        foreach (StructFieldSchema field in ValidateFields)
        {
            if (field.SchemaType is null) continue;

            if (jObject.ContainsKey(field.Name) && !jObject[field.Name].IsEmpty())
            {
                (Node.DataNode? v, JsonNode? e) = await field.SchemaType.ValidateValueAsync(context, jObject[field.Name]!);
                if (e != null && !e.IsEmpty())
                {
                    error ??= new JsonObject();
                    error[field.Name] = e;
                }
                else
                {
                    result[field.Name] = v;

                    // Field-level constraint validation with relation overrides
                    if (v != null && field.Constraints is { Length: > 0 })
                    {
                        foreach (var constraint in field.Constraints)
                        {
                            // Check if there's a relation that provides an override value for this constraint property
                            Node.DataNode? overrideVal = null;
                            var relation = _relations?.FirstOrDefault(r =>
                                r.Field.Equals(field.Name, StringComparison.OrdinalIgnoreCase) &&
                                r.Prop.Equals(constraint.Name, StringComparison.OrdinalIgnoreCase));

                            if (relation?.FuncNode != null)
                                overrideVal = await ResolveRelationNodeAsync(context, relation, result);

                            bool? valid = v switch
                            {
                                ScalarNode scalar => await constraint.ValidateScalarAsync(context, scalar, result, overrideVal),
                                EnumNode enumNode => await constraint.ValidateEnumAsync(context, enumNode, result, overrideVal),
                                StructNode structNode => await constraint.ValidateStructAsync(context, structNode, result, overrideVal),
                                ArrayNode arrayNode => await constraint.ValidateArrayAsync(context, arrayNode, result, overrideVal),
                                _ => null
                            };

                            if (valid == false)
                            {
                                error ??= new JsonObject();
                                error[field.Name] = TYPE_VALUE_NOT_VALID;
                                break;
                            }
                        }
                    }
                }
            }
            else if (field.Unpack ?? false)
            {
                additionalField = field.Name;
            }
            else if (field.Require ?? false)
            {
                StructRelationSchema? r = _relations?.FirstOrDefault(r => 
                    r.Field.Equals(field.Name, StringComparison.OrdinalIgnoreCase) &&
                    r.Prop.Equals(PROPERTY_DEFAULT, StringComparison.OrdinalIgnoreCase));

                // Complete by relation
                if (r != null)
                {
                    r.FuncNode ??=  await context.GetNodeTypeAsync<FunctionType>(r.Func);
                    if (r.FuncNode != null)
                    {
                        object?[] args = new Object[r.Args.Length];
                        for (int k = 0; k < r.Args.Length; k++)
                        {
                            FuncCallArg arg = r.Args[k];
                            if (!string.IsNullOrEmpty(arg.Name))
                            {
                                args[k] = result.GetValueByPaths(arg.Name.Split('.', StringSplitOptions.RemoveEmptyEntries));
                            }
                            else
                            {
                                args[k] = (object?)arg.SchemeType?.CreateNode(arg.Value) ?? arg.Value!;
                            }
                        }
                        result[field.Name] = await r.FuncNode.CallAsync<JsonNode>(context, args);
                        if (!result.GetField(field.Name)!.IsEmpty)
                            continue;
                    }
                }
                
                error ??= new JsonObject();
                error[field.Name] = TYPE_VALUE_STRUCT_MEMBER_REQUIRE;
            }
        }

        if (additionalField != null)
        {
            string[] fieldsName = _fields.Select(f => f.Name).ToArray();
            JsonObject additionalData = new();
            foreach (var kv in jObject)
            {
                if (kv.Value != null && !kv.Value.IsEmpty() && !fieldsName.Any(f => f.Equals(kv.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    additionalData[kv.Key] = kv.Value.DeepClone();
                }
            }

            var jsonNode = result.GetField(additionalField);
            if (jsonNode != null) 
                jsonNode.Value = additionalData;
        }

        // Union validation
        if (error == null && _unionValids is { Length: > 0 })
        {
            foreach(var r in _unionValids)
            {
                var valid = r.FuncNode ?? await context.GetNodeTypeAsync<FunctionType>(r.Func);
                if (valid == null) continue;
                var args = new object?[r.Args.Length];
                string? first = null;
                for(int j = 0; j < r.Args.Length; j++)
                {
                    var arg = r.Args[j];
                    if (!string.IsNullOrWhiteSpace(arg.Name))
                    {
                        args[j] = result.GetValueByPaths(arg.Name);
                        first ??= arg.Name.Split('.').FirstOrDefault();
                    }
                    else
                    {
                        args[j] = await context.GetSchemaNodeAsync(arg.SchemeType, arg.Value);
                    }
                }
                if (!string.IsNullOrWhiteSpace(first) && !await valid.CallAsync<bool>(context, args))
                {
                    error ??= [];
                    error[first] = TYPE_VALUE_NOT_VALID;
                }
            }
        }

        // Constraint validation
        if (error == null && Constraints is { Length: > 0 })
        {
            foreach (IConstraintProperty constraint in Constraints)
            {
                if (await constraint.ValidateStructAsync(context, result) == false)
                    return (null, TYPE_VALUE_NOT_VALID);
            }
        }

        return (result, error);
    }

    /// <inheritdoc />
    public override bool CanBeUseAs(NodeType other, bool exactly = false)
    {
        if (Name.Equals(NS_SYSTEM_STRUCT) || other.Name.Equals(NS_SYSTEM_STRUCT) || base.CanBeUseAs(other, exactly)) return true;
        if (other is not StructType @struct) return false;
        return @struct._fields.Any(v => _fields.Any(f => f.Name.Equals(v.Name, StringComparison.OrdinalIgnoreCase))) 
               && @struct._fields.All(v =>
               {
                   StructFieldSchema? match = _fields.FirstOrDefault(f => f.Name.Equals(v.Name, StringComparison.OrdinalIgnoreCase));
                   return match?.SchemaType == null ? !(v.Require ?? false) : v.SchemaType != null && match.SchemaType.CanBeUseAs(v.SchemaType);
               });
    }
    
    /// <summary>
    /// Resolve a relation function call to get an override value as AnySchemaNode, using already validated struct fields as arguments.
    /// </summary>
    private static async Task<Node.DataNode?> ResolveRelationNodeAsync(
        SchemaContext context, RelationSchema relation, StructNode result)
    {
        if (relation.FuncNode == null) return null;

        var args = new object?[relation.Args.Length];
        for (int k = 0; k < relation.Args.Length; k++)
        {
            CallArg arg = relation.Args[k];
            if (!string.IsNullOrEmpty(arg.Name))
                args[k] = result.GetValueByPaths(arg.Name.Split('.', StringSplitOptions.RemoveEmptyEntries));
            else
                args[k] = (object?)arg.SchemeType?.CreateNode(arg.Value) ?? arg.Value!;
        }

        return await relation.FuncNode.CallAsync<Node.DataNode>(context, args);
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
                context.LogWarning($"Failed to load ref type '{name}' for property '{prop.GetType().GetPropertyName()}' in struct field '{field.Name}'");
            }
        }
        
        // init
        Name = field.Name;
        Type = valueType;
        Properties = props;
        Constraints = constraints;
        RefTypes = refTypes?.ToArray();
        Require = props.FirstOrDefault(p => p is Require) is Require r ? r.Value : null;
        DisplayOnly = props.FirstOrDefault(p => p is DisplayOnly) is DisplayOnly d ? d.Value : null;
        Unpack = props.FirstOrDefault(p => p is Unpack) is Unpack u ? u.Value : null;
        Default = props.FirstOrDefault(p => p is Default) is Default defProp ? await valueType.ValidateValueAsync(context, defProp.Value) : null;
        UpLimit = props.FirstOrDefault(p => p.GetType().GetPropertyName().Equals(nameof(UpLimit), StringComparison.OrdinalIgnoreCase)) is IConstraintProperty up ? up.GetValue<object>() : null;
        LowLimit = props.FirstOrDefault(p => p.GetType().GetPropertyName().Equals(nameof(LowLimit), StringComparison.OrdinalIgnoreCase)) is IConstraintProperty low ? low.GetValue<object>() : null;
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