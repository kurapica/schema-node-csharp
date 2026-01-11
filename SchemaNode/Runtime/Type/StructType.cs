using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory struct schema representation
/// </summary>
public class StructType: AnySchemaType
{
    #region Data
    
    /// <summary>
    /// The base struct type to be inherited from.
    /// </summary>
    public string? Base { get; set; }

    /// <summary>
    /// The struct fields
    /// </summary>
    public StructFieldConfig[] Fields { get; set; } = [];
    
    /// <summary>
    /// The relations between the fields
    /// </summary>
    public StructFieldRelation[]? Relations { get; set; }
    
    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
    
    #endregion
    
    #region State
    
    /// <inheritdoc />
    public override SchemaType Type => SchemaType.Struct;
    
    #endregion
    
    #region Ref
    
    /// <summary>
    /// The base struct node
    /// </summary>
    public StructType? BaseNode { get; set; }
    
    #endregion
    
    #region Methods

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        StructSchema? @struct = schema.Struct;
        
        // Data
        Base = @struct?.Base;
        Fields = @struct?.Fields ?? [];
        Relations = @struct?.Relations ?? [];
        Additional = @struct?.Additional;
        
        // Status
        if (@struct == null) Status = SchemaNodeStatus.NoDefinition;
        
        // Ref
        if (!string.IsNullOrWhiteSpace(Base))
        {
            AnySchemaType? baseNode = await context.GetSchemaTypeAsync(Base, preload: preload);
            if (baseNode is not StructType node)
                Status = SchemaNodeStatus.StructWrongBase;
            else
            {
                BaseNode = node;
                node.AddRef(this);
            }
        }
        
        // Load Fields
        foreach (StructFieldConfig field in Fields)
        {
            AnySchemaType? schemaType = await context.GetSchemaTypeAsync(field.Type, preload: preload);
            if (schemaType == null || schemaType.Type is SchemaType.Namespace or SchemaType.Func && !Regex.IsMatch(field.Type, REGEX_GENERIC_TYPE))
            {
                field.Status = SchemaNodeStatus.StructMemberWrongType;
                Status = SchemaNodeStatus.StructMemberWrongType;
                continue;
            }

            field.Status = null;
            field.SchemeType = schemaType;
            schemaType.AddRef(this);
        }
        
        // Load Relation
        if (Relations != null)
        {
            foreach (StructFieldRelation relation in Relations)
            {
                AnySchemaType? funcNode = await context.GetSchemaTypeAsync(relation.Func, preload: preload);
                if (funcNode is not FunctionType node)
                {
                    relation.Status = SchemaNodeStatus.StructRelationshipWrongFunc;
                    Status = SchemaNodeStatus.StructRelationshipWrongFunc;
                    continue;
                }
                relation.Status = null;
                relation.FuncNode = node;
                node.AddRef(this);
            }
        }
    }

    /// <inheritdoc />
    public override void Release()
    {
        BaseNode?.RemoveRef(this);
        BaseNode = null;
        foreach (StructFieldConfig config in Fields)
        {
            config.SchemeType?.RemoveRef(this);
            config.SchemeType = null;
        }

        if (Relations != null)
        {
            foreach (StructFieldRelation relation in Relations)
            {
                relation.FuncNode?.RemoveRef(this);
                relation.FuncNode = null;
            }
        }
        
        // Gets relative struct types
        List<AnySchemaType> relTypes = [this];
        
        if (UsedBy is { Count: > 0 })
            relTypes.AddRange(UsedBy.Keys.Where(p => p.Type == SchemaType.Struct));
        foreach (AnySchemaType node in relTypes.ToList().Where(node => node.UsedBy is { Count: > 0 }))
            relTypes.AddRange(node.UsedBy!.Keys.Where(p => p.Type == SchemaType.Array));

        // Gets the relative field type
        foreach (AppFieldType field in relTypes.Where(node => node.UsedByApp is { Count: > 0 }).SelectMany(node => node.UsedByApp!.Keys))
            field.Schema = null; // Clear to reload
    }

    /// <inheritdoc />
    public override async Task<(AnySchemaNode? value, JsonNode? error)> ValidateValueAsync(SchemaContext context, JsonNode value)
    {
        if (value is not JsonObject jobject)
            return (null, TYPE_VALUE_NOT_VALID);
        
        // validate fields
        StructTypeNode result = new(this);
        JsonObject? error = null;
        foreach (StructFieldConfig field in Fields)
        {
            if (field.DisplayOnly ?? false) continue;
            if (field.SchemeType is null) continue;

            if (jobject.ContainsKey(field.Name) && !jobject[field.Name].IsEmpty())
            {
                (AnySchemaNode? v, JsonNode? e) = await field.SchemeType.ValidateValueAsync(context, jobject[field.Name]!);
                if (e != null && !e.IsEmpty())
                {
                    error ??= new JsonObject();
                    error[field.Name] = e;
                }
                else
                {
                    result[field.Name] = v;
                }
            }
            else if (field.Require ?? false)
            {
                error ??= new JsonObject();
                error[field.Name] = TYPE_VALUE_STRUCT_MEMBER_REQUIRE;
            }
        }
        
        // @TODO: Union validation
        return (result, error);
    }

    /// <inheritdoc />
    public override bool CanBeUseAs(AnySchemaType other)
    {
        if (base.CanBeUseAs(other) || Name.Equals(NS_SYSTEM_STRUCT) || other.Name.Equals(NS_SYSTEM_STRUCT)) return true;
        if (other is not StructType @struct) return false;
        StructType? baseNode = BaseNode;
        while (baseNode != null && baseNode != @struct) baseNode = baseNode.BaseNode;
        return baseNode == @struct || 
               @struct.Fields.Any(v => Fields.Any(f => f.Name.Equals(v.Name, StringComparison.OrdinalIgnoreCase))) 
               && @struct.Fields.All(v =>
               {
                   StructFieldConfig? match = Fields.FirstOrDefault(f => f.Name.Equals(v.Name, StringComparison.OrdinalIgnoreCase));
                   return match?.SchemeType == null ? !(v.Require ?? false) : v.SchemeType != null && match.SchemeType.CanBeUseAs(v.SchemeType);
               });
    }

    public override IEnumerable<AnySchemaType> GetDependNodes()
    {
        if (BaseNode != null) yield return BaseNode;
        foreach (StructFieldConfig field in Fields)
        {
            if (field.SchemeType != null)
                yield return field.SchemeType;
        }
        if (Relations != null)
        {
            foreach (StructFieldRelation relation in Relations)
            {
                if (relation.FuncNode != null)
                    yield return relation.FuncNode;
            }
        }
    }

    public IReadOnlyList<PropertyInfo>? GetCSharpProperties(bool primary = false)
    {
        return GetStructFieldCSharpProperties(Name, primary);
    }
    
    /// <summary>
    /// Gets the field by name
    /// </summary>
    public StructFieldConfig? GetField(string fieldName) 
        => Fields.FirstOrDefault(f => f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));

    #endregion

    #region Static Feature

    /// <summary>
    /// Generate system enum
    /// </summary>
    public static NodeSchema[] GenerateSystemStruct(Type type, string? ns = null)
    {
        if (type is { IsClass: false, IsValueType: false } || 
            type is { IsClass: true, IsAbstract: true } ||
            (type.IsValueType && type.IsPrimitiveLike())) return [];
        
        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(p =>
            p.GetMethod?.IsPrivate != true &&
            p.GetCustomAttribute<NotMappedAttribute>() == null &&
            p is { CanRead: true, CanWrite: true } && (
                p.GetCustomAttribute<SchemaAttribute>() != null ||
                !string.IsNullOrWhiteSpace(p.PropertyType.GetSchemaType(true)))
            ).OrderBy(p => p.MetadataToken).ToArray();
        if (properties.Length == 0) return [];

        List<PropertyInfo> fieldMaps = [];
        string[] primarys = [];
        Dictionary<string, string[]> indexes = [];
        SchemaAttribute? typeAttr = type.GetCustomAttribute<SchemaAttribute>();
        string typeName = typeAttr?.Name ?? $"{(string.IsNullOrWhiteSpace(ns) ? "" : $"{ns}.")}{type.Name.ToLowerInvariant()}";

        NodeSchema structSchema = new ()
        {
            Name = typeName,
            Type = SchemaType.Struct,
            Display = typeAttr?.Display ?? type.GetSummaryFromXmlDoc() ?? typeName,
            Struct = new StructSchema
            {
                Fields = properties.Select(p =>
                {
                    fieldMaps.Add(p);

                    SchemaAttribute? fieldAttr = p.GetCustomAttribute<SchemaAttribute>();
                    string fieldName = p.Name.ToCamelCase();
                    StructFieldConfig config = new ()
                    {
                        Name = fieldName,
                        Type = fieldAttr?.Name ?? p.PropertyType.GetSchemaType(defaultNs: ns)!,
                        Require = p.GetCustomAttribute<RequiredAttribute>() != null,
                        Display = fieldAttr?.Display ?? type.GetSummaryFromXmlDoc(p) ?? $"{typeName}.{fieldName}",
                    };

                    // limit check
                    if (config.Type == NS_SYSTEM_STRING)
                    {
                        StringLengthAttribute? strLenAttr = p.GetCustomAttribute<StringLengthAttribute>();
                        MaxLengthAttribute? maxLengthAttribute = p.GetCustomAttribute<MaxLengthAttribute>();
                        config.UpLimit = (strLenAttr?.MaximumLength ?? maxLengthAttribute?.Length)?.ToString();
                        config.LowLimit = (strLenAttr?.MinimumLength ?? 0).ToString();
                    }
                    else
                    {
                        RangeAttribute? rangeAttr = p.GetCustomAttribute<RangeAttribute>();
                        if (rangeAttr != null)
                        {
                            config.LowLimit = rangeAttr.Minimum.ToLiteral();
                            config.UpLimit = rangeAttr.Maximum.ToLiteral();
                        }
                    }

                    // index check
                    foreach(IndexAttribute attr in p.GetCustomAttributes<IndexAttribute>())
                    {
                        if (string.IsNullOrEmpty(attr.Name))
                        {
                            // primary
                            if (attr.Order >= primarys.Length)
                                Array.Resize(ref primarys, attr.Order + 1);

                            if (string.IsNullOrWhiteSpace(primarys[attr.Order]))
                            {
                                // with given order
                                primarys[attr.Order] = config.Name;
                            }
                            else
                            {
                                // follow default order
                                Array.Resize(ref primarys, primarys.Length + 1);
                                primarys[^1] = config.Name;
                            }
                        }
                        else
                        {
                            // normal index
                            if (!indexes.ContainsKey(attr.Name)) indexes[attr.Name] = [];
                            string[] fields = indexes[attr.Name];
                            if (attr.Order >= fields.Length)
                                Array.Resize(ref fields, attr.Order + 1);

                            if (string.IsNullOrWhiteSpace(fields[attr.Order]))
                            {
                                // with given order
                                fields[attr.Order] = config.Name;
                            }
                            else
                            {
                                // follow default order
                                Array.Resize(ref fields, fields.Length + 1);
                                fields[^1] = config.Name;
                            }
                            indexes[attr.Name] = fields;
                        }
                    }

                    return config;
                }).ToArray()
            }
        };
        CsharpTypeProperties[structSchema.Name.ToLower()] = fieldMaps;
        
        if (primarys.Length == 0) return [structSchema];
        CSharpTypePrimaryProperties[structSchema.Name.ToLower()] = primarys.Select(p => fieldMaps.First(f => f.Name.Equals(p, StringComparison.OrdinalIgnoreCase))).ToArray();

        NodeSchema arraySchema = new NodeSchema
        {
            Name = $"{structSchema.Name}s",
            Type = SchemaType.Array,
            Display = $"{Locale.LIST_PREFIX}{{@{structSchema.Name}}}{Locale.LIST_SUFFIX}",
            Array = new ArraySchema
            {
                Element = structSchema.Name,
                Primary = primarys,
                Indexes = indexes.Select(kv => new DataIndex
                {
                    Name = kv.Key,
                    Fields = kv.Value
                }).ToArray()
            }
        };
        return [structSchema, arraySchema];
    }
    
    /// <summary>
    /// Gets the C# properties for the struct type
    /// </summary>
    internal static IReadOnlyList<PropertyInfo>? GetStructFieldCSharpProperties(string type, bool primary = false) => (primary ? CSharpTypePrimaryProperties : CsharpTypeProperties).GetValueOrDefault(type.GetBaseType().ToLower());
    static readonly ConcurrentDictionary<string, IReadOnlyList<PropertyInfo>> CsharpTypeProperties = [];
    static readonly ConcurrentDictionary<string, IReadOnlyList<PropertyInfo>> CSharpTypePrimaryProperties = [];

    #endregion
    
    #region Conversion
    
    /// <summary>
    /// Convert the node to schema
    /// </summary>
    public static implicit operator NodeSchema?(StructType? schema)
    {
        return schema?.ToSchema().With(new StructSchema
        {
            Base = schema.Base,
            Relations = schema.Relations,
            Fields = schema.Fields,
            Additional = schema.Additional,
        });
    }
    
    #endregion
    
    #region Generic Struct Implementations

    /// <summary>
    /// Get the generic struct type
    /// </summary>
    public async Task<StructType?> GetGenericTypeAsync(SchemaContext context, string[] types)
    {
        string[] generics = Fields.Where(f => f.SchemeType is GenericType).Select(f => f.Type).Distinct().ToArray();
        if (generics.Length == 0 || generics.Length != types.Length) return null; // Not a generic struct or not match

        _genericTypes ??= new ConcurrentDictionary<string, StructType>();
        string key = string.Join('|', types);
        if (_genericTypes.TryGetValue(key, out StructType? type)) return type;
        
        // Generate new struct type
        StructType newStruct = new()
        {
            Name = $"{Name}<{string.Join(',', types)}>",
            Display = $"{Locale.LIST_PREFIX}{string.Join(",", types.Select(t => $"{{@{t}}}"))}{Locale.LIST_SUFFIX}",
            Base = Name,
            Fields = new StructFieldConfig[Fields.Length],
            Namespace = Namespace,
            Relations = Relations
        };

        for (int i = 0; i < Fields.Length; i++)
        {
            StructFieldConfig f = Fields[i];
            
            if (f.SchemeType is GenericType)
            {
                StructFieldConfig copy = f.ToJsonNode()!.FromJson<StructFieldConfig>()!;
                int index = Array.IndexOf(generics, f.Type);
                copy.Type = types[index];
                AnySchemaType? schemaType = await context.GetSchemaTypeAsync(copy.Type);
                if (schemaType == null || schemaType.Type is SchemaType.Namespace or SchemaType.Func)
                {
                    return null;
                }
                copy.SchemeType = schemaType;
                schemaType.AddRef(newStruct);
                newStruct.Fields[i] = copy;
            }
            else
            {
                newStruct.Fields[i] = f;
            }
        }
        
        return _genericTypes.GetOrAdd(key, newStruct);
    }

    private ConcurrentDictionary<string, StructType>? _genericTypes;

    #endregion
}
