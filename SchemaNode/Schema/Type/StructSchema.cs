using SchemaNode.Attribute;
using SchemaNode.Components;
using SchemaNode.Components.Property;
using SchemaNode.Components.Property.Constraint;
using SchemaNode.Components.Property.Presentation;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using static SchemaNode.Utility.Constant;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/// <summary>
/// The struct schema.
/// </summary>
[SchemaApp]
[Schema($"{NS_SYSTEM_SCHEMA_DEF_STRUCT}.schema")]
public sealed class StructSchema: IAdditionalProperty
{
    /// <summary>
    /// The struct name
    /// </summary>
    [Index]
    [JsonIgnore]
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Name { get; set; }
        
    /// <summary>
    /// The struct fields
    /// </summary>
    public StructFieldSchema[] Fields { get; set; } = [];
    
    /// <summary>
    /// The relations between the fields
    /// </summary>
    public StructRelationSchema[]? Relations { get; set; }

    /// <summary>
    /// The union validations
    /// </summary>
    public StructUnionValidation[]? UnionValids { get; set; }

    /// <summary>
    /// The atomic flag indicates whether the array is atomic, which means that the array should be treated as a whole when performing operations such as updates, delete or render.
    /// </summary>
    public bool? Atomic { get; set; }

    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }

    /// <summary>
    /// Used to combine custom schema to system schema
    /// </summary>
    internal void CombineCustomSchema(StructSchema? other)
    {
        if (other == null) return;
        Relations = other.Relations ?? Relations;
        this.CombineAdditionalProperty(other);

        foreach(StructFieldSchema field in Fields)
        {
            field.CombineCustomSchema(other.Fields.FirstOrDefault(f => f.Name.Equals(field.Name, StringComparison.OrdinalIgnoreCase)));
        }
    }
}

/// <summary>
/// The struct field config
/// </summary>
[Schema($"{NS_SYSTEM_SCHEMA_DEF_STRUCT}.field")]
public class StructFieldSchema: IAdditionalProperty
{
    /// <summary>
    /// The field name
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The type name of the node.
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    [Schema(NS_SYSTEM_SCHEMA_TYPE_RULE_VALUE)]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// The label of the node.
    /// </summary>
    public LocaleString? Display { get; set; }

    /// <summary>
    /// The schema node status
    /// </summary>
    [NotMapped]
    public SchemaNodeStatus? Status { get; set; }

    #region Additional

    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }

    /// <summary>
    /// The properties
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    internal IProperty[]? Properties { get; set; }

    /// <summary>
    /// The constraint properties from Additional
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    internal IConstraintProperty[]? Constraints { get; set; }

    /// <summary>
    /// The ref types from the properties in Additional
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    internal List<AnySchemaType>? RefTypes { get; set; }

    #endregion

    #region Properties

    /// <summary>
    /// The description of the node.
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public LocaleString? Desc => Properties?.FirstOrDefault(p => p is DescProperty) is DescProperty desc ? desc.Value : null;

    /// <summary>
    /// The node data is required.
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public bool? Require { get; private set; }

    /// <summary>
    /// The node should be display only, won't be submitted.
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public bool? DisplayOnly { get; private set; }

    /// <summary>
    /// Unpack/pack additional data for the json node.
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public bool? Unpack { get; private set; }

    /// <summary>
    /// The default value of the node.
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public AnySchemaNode? Default { get; private set; }

    /// <summary>
    /// The low limit of the scalar value.
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public object? LowLimit { get; private set; }

    /// <summary>
    /// The up limit of the scalar value.
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public object? UpLimit { get; private set; }

    #endregion
                
    #region Ref
    
    /// <summary>
    /// The type node ref
    /// </summary>
    [JsonIgnore]
    [NotMapped]
    public AnySchemaType? SchemeType { get; set; }

    #endregion

    #region Methods

    internal async Task LoadFieldSchema(SchemaContext context, StructType @struct, bool preload = false)
    {
        Status = null;
        AnySchemaType? schemaType = await context.GetSchemaTypeAsync(Type, preload: preload);
        if (schemaType == null || schemaType.Type is SchemaType.Namespace or SchemaType.Func && !Regex.IsMatch(Type, REGEX_GENERIC_TYPE))
        {
            Status = SchemaNodeStatus.StructMemberWrongType;
            return;
        }

        SchemeType = schemaType;
        schemaType.AddRef(@struct);

        Properties = null;
        Constraints = null;
        RefTypes = null;

        if (Additional != null)
        {
            Properties = PropertyType.GetProperties<IProperty>(context, SchemaType.StructField, Additional, SchemeType)?.ToArray();

            if (Properties is { Length: > 0 })
            {
                Constraints = Properties.Where(p => p is IConstraintProperty).Cast<IConstraintProperty>().ToArray();
                foreach (var typeRef in Properties.Where(p => p is ITypeRefProperty).Cast<ITypeRefProperty>())
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
        Require = Properties?.FirstOrDefault(p => p is RequireProperty) is RequireProperty r ? r.Value : null;
        DisplayOnly = Properties?.FirstOrDefault(p => p is DisplayOnlyProperty) is DisplayOnlyProperty d ? d.Value : null;
        Unpack = Properties?.FirstOrDefault(p => p is UnpackProperty) is UnpackProperty u ? u.Value : null;
        Default = Properties?.FirstOrDefault(p => p is DefaultProperty) is DefaultProperty def ? def.Value : null;
        UpLimit = Properties?.FirstOrDefault(p => p.Name.Equals(PROPERTY_UPLIMIT, StringComparison.OrdinalIgnoreCase)) is IConstraintProperty up ? up.GetValue<object>() : null;
        LowLimit = Properties?.FirstOrDefault(p => p.Name.Equals(PROPERTY_LOWLIMIT, StringComparison.OrdinalIgnoreCase)) is IConstraintProperty low ? low.GetValue<object>() : null;
    }

    internal void UnloadFieldSchema(StructType @struct)
    {
        if (SchemeType != null) SchemeType.RemoveRef(@struct);
        if (RefTypes != null)
        {
            foreach (AnySchemaType type in RefTypes)
            {
                type.RemoveRef(@struct);
            }
        }
        Properties = null;
        Constraints = null;
        RefTypes = null;
        Status = null;

        Require = null;
        DisplayOnly = null;
        Unpack = null;
        Default = null;
        UpLimit = null;
        LowLimit = null;
    }

    /// <summary>
    /// Used to combine custom schema to system schema
    /// </summary>
    internal void CombineCustomSchema(StructFieldSchema? other)
    {
        if (other == null) return;
        Display = Display != null ? Display.Concat(other.Display) : other.Display;
        this.CombineAdditionalProperty(other);
    }

    /// <summary>
    /// Gets the up limit
    /// </summary>
    public T? GetUplimit<T>() where T : struct
    {
        if (UpLimit == null) return null;
        object? uplimit = Utility.Extension.TryConvert(typeof(T), UpLimit);
        if (uplimit == null) return null;
        return (T)uplimit;
    }

    /// <summary>
    /// Gets the low limit
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T? GetLowlimit<T>()
    {
        if (LowLimit == null) return default;
        object? lowlimit = Utility.Extension.TryConvert(typeof(T), LowLimit);
        if (lowlimit == null) return default;
        return (T)lowlimit;
    }

    #endregion
}

/// <summary>
/// The relation between fields
/// </summary>
[Schema($"{NS_SYSTEM_SCHEMA_DEF_STRUCT}.relation")]
public class StructRelationSchema
{
    /// <summary>
    /// The target field, can use . for deep fields
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public required string Field { get; set; }

    /// <summary>
    /// The property of the realtion, so the function can modify it dynamically
    /// </summary>
    [Schema(NS_SYSTEM_SCHEMA_PROPERTY)]
    public required string Property { get; set; }

    /// <summary>
    /// The relation function
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    [Schema(NS_SYSTEM_SCHEMA_TYPE_FUNC)]
    public required string Func { get; set; } = string.Empty;

    /// <summary>
    /// The func arguments
    /// </summary>
    public FuncCallArg[] Args { get; set; } = [];

    /// <summary>
    /// The schema node status
    /// </summary>
    [NotMapped]
    public SchemaNodeStatus? Status { get; set; }

    /// <summary>
    /// The function node ref
    /// </summary>
    [JsonIgnore]
    [NotMapped]
    public FunctionType? FuncNode { get; set; }
}

public class StructUnionValidation
{
    /// <summary>
    /// The union valiation func
    /// </summary>
    [Schema(NS_SYSTEM_SCHEMA_TYPE_RULE_UNIONVALID)]
    public string Func { get; set; } = string.Empty;

    /// <summary>
    /// The func arguments
    /// </summary>
    public FuncCallArg[] Args { get; set; } = [];

    /// <summary>
    /// The schema node status
    /// </summary>
    [NotMapped]
    public SchemaNodeStatus? Status { get; set; }

    /// <summary>
    /// The function node ref
    /// </summary>
    [JsonIgnore]
    [NotMapped]
    public FunctionType? FuncNode { get; set; }
}