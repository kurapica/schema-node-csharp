using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Presentation;
using SchemaNode.Property.Record;
using SchemaNode.Property.Schema;
using SchemaNode.Runtime;
using SchemaNode.Scalar;
using SchemaNode.Service;
using SchemaNode.Struct;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using NodeType = SchemaNode.Property.Schema.NodeType;

namespace SchemaNode.Schema;

/// <summary>
/// The struct schema
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_STRUCT, SCHEMA_KIND_ORDER_STRUCT)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_STRUCT, SCHEMA_KIND_ORDER_STRUCT)]
[Meta<ValueSchemaKind>(SCHEMA_KIND_STRUCT, SCHEMA_KIND_ORDER_STRUCT)]
[Meta<NodeType>(typeof(StructType))]
[Meta<SchemaGenerator>(typeof(StructGenerator))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_STRUCT}.schema")]
public sealed class StructSchema : ExtensibleSchema
{
    /// <summary>
    /// The struct fields
    /// </summary>
    public StructFieldSchema[] Fields { get; set; } = [];
    
    /// <summary>
    /// The union validations
    /// </summary>
    public StructUnionValidation[]? UnionValids { get; set; }

    public override void CombineExtensions(ExtensibleSchema? other, ISchemaRuntime? runtime = null)
    {
        if (other is not StructSchema otherStruct) return; 
        base.CombineExtensions(otherStruct, runtime);

        // Find the one that contains another
        if (Fields.All(f => otherStruct.Fields.Any(of => f.Name.Equals(of.Name, StringComparison.OrdinalIgnoreCase))))
        {
            foreach (StructFieldSchema field in otherStruct.Fields)
            {
                StructFieldSchema? match = Fields.FirstOrDefault(x => x.Name == field.Name);
                if (match is not null)
                    field.CombineExtensions(match, runtime);
            }
            Fields = otherStruct.Fields;
        }
        else
        {
            foreach (StructFieldSchema field in Fields)
            {
                StructFieldSchema? match = otherStruct.Fields.FirstOrDefault(x => x.Name == field.Name);
                if (match is not null)
                    field.CombineExtensions(match, runtime);
            }
        }
        
        // Combine the union valids
        if (otherStruct.UnionValids is { Length: > 0 })
        {
            if (UnionValids is null || UnionValids.Length == 0)
            {
                UnionValids = otherStruct.UnionValids[..];
            }
            else
            {
                List<StructUnionValidation> combined = new List<StructUnionValidation>(UnionValids);
                foreach (StructUnionValidation union in otherStruct.UnionValids)
                {
                    if (union.Args.All(a => !a.Value.IsEmpty() || 
                                            a.Source?.Split('.').FirstOrDefault() is { } source &&
                                            Fields.Any(f => f.Name.Equals(source, StringComparison.OrdinalIgnoreCase)))
                            && combined.All(f => !f.Equals(union)))
                        combined.Add(union);
                }
                UnionValids = combined.ToArray();
            }
        }
    }
}

/// <summary>
/// Declare struct property for node schema
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(NodeSchema.Kind)}", SCHEMA_KIND_STRUCT)]
public sealed class StructProperty: Property<StructSchema>;

/// <summary>
/// Represents the struct type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_STRUCT}.type")]
public class StructType: AnyType;

/// <summary>
/// The struct field schema
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_STRUCT}.field")]
[Meta<SchemaKind>(SCHEMA_KIND_STRUCT_FIELD, SCHEMA_KIND_ORDER_STRUCT_FIELD)]
public sealed class StructFieldSchema : ExtensibleSchema
{
    /// <summary>
    /// The field name
    /// </summary>
    [Meta<SchemaType>(typeof(Identifier))]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The type name of the node.
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    public string Type { get; set; } = string.Empty;

    public override bool Equals(ExtensibleSchema? other)
    {
        if (other is not StructFieldSchema otherField) return false;
        return ReferenceEquals(this, otherField) || Name.Equals(otherField.Name, StringComparison.OrdinalIgnoreCase);
    }

    #region Runtime
  
    /// <summary>
    /// The type node ref
    /// </summary>
    [JsonIgnore]
    [SchemaIgnore]
    internal Runtime.ValueType? NodeType { get; set; }
    
    /// <summary>
    /// The properties
    /// </summary>
    [JsonIgnore]
    [SchemaIgnore]
    internal IProperty[]? Properties { get; set; }

    /// <summary>
    /// The constraint properties from Extensions
    /// </summary>
    [JsonIgnore]
    [SchemaIgnore]
    internal IConstraintProperty[]? Constraints { get; set; }

    /// <summary>
    /// The ref types from the properties in Extensions
    /// </summary>
    [JsonIgnore]
    [SchemaIgnore]
    internal Runtime.NodeType[]? RefTypes { get; set; }
    
    /// <summary>
    /// The node data is required.
    /// </summary>
    [SchemaIgnore]
    [JsonIgnore]
    public bool? Require { get; private set; }

    /// <summary>
    /// The node should be display only, won't be submitted.
    /// </summary>
    [SchemaIgnore]
    [JsonIgnore]
    public bool? DisplayOnly { get; private set; }

    /// <summary>
    /// Unpack/pack additional data for the json node.
    /// </summary>
    [SchemaIgnore]
    [JsonIgnore]
    public bool? Unpack { get; private set; }

    /// <summary>
    /// The default value of the node.
    /// </summary>
    [SchemaIgnore]
    [JsonIgnore]
    public object? Default { get; private set; }

    /// <summary>
    /// The low limit of the scalar value.
    /// </summary>
    [SchemaIgnore]
    [JsonIgnore]
    public object? LowLimit { get; private set; }

    /// <summary>
    /// The up limit of the scalar value.
    /// </summary>
    [SchemaIgnore]
    [JsonIgnore]
    public object? UpLimit { get; private set; }

    // Load field schema    
    internal async Task LoadFieldSchema(SchemaContext context, Runtime.StructType @struct, GenericParameter[]? genericParameters = null)
    {
        Error = null;
        if (await context.GetNodeTypeAsync(Type, genericParameters) is not Runtime.ValueType nodeType)
        {
            Error = ErrorCodes.STRUCT_FIELD_WRONG_TYPE;
            return;
        }

        NodeType = nodeType;
        nodeType.AddRef(@struct);

        Properties = null;
        Constraints = null;
        RefTypes = null;

        // Collect property names referenced by relations for this field
        var relationProps = @struct.Relations?
            .Where(rs => rs.Target.Equals(Name, StringComparison.OrdinalIgnoreCase))
            .Select(rs => rs.Property).ToArray();

        if (Extensions != null || relationProps?.Any() == true)
        {
            Properties = PropertyType.GetProperties<IProperty>(context, Enum.SchemaType.StructField, Extensions ?? new(), SchemaType, relationProps)?.ToArray();

            if (Properties is { Length: > 0 })
            {
                Constraints = Properties.Where(p => p is IConstraintProperty).Cast<IConstraintProperty>().ToArray();
                foreach (var typeRef in Properties.Where(p => p is ITypeRefProperty && p.HasValue).Cast<ITypeRefProperty>())
                {
                    string? name = typeRef.GetValue<string>();
                    AnySchemaType? node = !string.IsNullOrWhiteSpace(name) ? await context.GetSchemaTypeAsync(name) : null;
                    if (node != null)
                    {
                        RefTypes ??= [];
                        RefTypes.Add(node);
                        node.AddRef(@struct);
                    }
                    else
                    {
                        Status = SchemaNodeStatus.WrongRefType;
                        context.LogWarning($"Failed to load ref type '{name}' for property '{typeRef.Name}' in schema '{Name}'");
                    }
                }
            }
        }

        // Cache
        Require = Properties?.FirstOrDefault(p => p is Require) is Require r ? r.Value : null;
        DisplayOnly = Properties?.FirstOrDefault(p => p is DisplayOnly) is DisplayOnly d ? d.Value : null;
        Unpack = Properties?.FirstOrDefault(p => p is Unpack) is Unpack u ? u.Value : null;
        Default = Properties?.FirstOrDefault(p => p is Default) is Default def ? def.Value : null;
        UpLimit = Properties?.FirstOrDefault(p => p.GetType().GetPropertyName().Equals(nameof(UpLimit), StringComparison.OrdinalIgnoreCase)) is IConstraintProperty up ? up.GetValue<object>() : null;
        LowLimit = Properties?.FirstOrDefault(p => p.GetType().GetPropertyName().Equals(nameof(LowLimit), StringComparison.OrdinalIgnoreCase)) is IConstraintProperty low ? low.GetValue<object>() : null;
    }

    internal void UnloadFieldSchema(StructType @struct)
    {
        if (NodeType != null) NodeType.RemoveRef(@struct);
        if (RefTypes != null)
        {
            foreach (var type in RefTypes)
            {
                type.RemoveRef(@struct);
            }
        }
        Properties = null;
        Constraints = null;
        RefTypes = null;
        Error = null;

        Require = null;
        DisplayOnly = null;
        Unpack = null;
        Default = null;
        UpLimit = null;
        LowLimit = null;
    }

    #endregion
}

[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_STRUCT}.unionvalid")]
public class StructUnionValidation: IEquatable<StructUnionValidation>
{
    /// <summary>
    /// The union validation func
    /// </summary>
    [Meta<SchemaType>(typeof(UnionValidFuncType))]
    public string Func { get; set; } = string.Empty;

    /// <summary>
    /// The func arguments
    /// </summary>
    public CallArg[] Args { get; set; } = [];

    /// <summary>
    /// The error message
    /// </summary>
    [SchemaIgnore]
    public string? Error { get; set; }

    /// <summary>
    /// The function node ref
    /// </summary>
    [JsonIgnore]
    [SchemaIgnore]
    public FunctionType? FuncNode { get; set; }
    
    /// <summary>
    /// Whether the schema is equals
    /// </summary>
    public bool Equals(StructUnionValidation? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Func == other.Func && Args.SequenceEqual(other.Args);
    }
}