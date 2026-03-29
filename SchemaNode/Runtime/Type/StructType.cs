using SchemaNode.Attribute;
using SchemaNode.Components.Property;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory struct schema representation
/// </summary>
public sealed class StructType: AnySchemaType
{
    #region Data
    
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
    /// The atomic flag indicates whether the struct is atomic, which means that the struct should be treated as a whole when performing operations such as updates, delete or render.
    /// </summary>
    public bool? Atomic { get; set; }

    #endregion

    #region State

    /// <inheritdoc />
    public override SchemaType Type => SchemaType.Struct;

    /// <summary>
    /// Is value type
    /// </summary>
    public override bool IsValueType => true;

    #endregion
        
    #region Methods

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        StructSchema? @struct = schema.Struct;
        
        // Data
        Fields = @struct?.Fields ?? [];
        Relations = @struct?.Relations ?? [];
        Atomic = @struct?.Atomic ?? false;
        
        // Status
        if (@struct == null) Status = SchemaNodeStatus.NoDefinition;
               
        // Load Fields
        foreach (StructFieldSchema field in Fields)
        {
            await field.LoadFieldSchema(context, this, preload);
            if (field.Status.HasValue && field.Status != SchemaNodeStatus.Ready)
            {
                Status = field.Status.Value;
            }
        }
        
        // Load Relation
        if (Relations != null)
        {
            foreach (StructRelationSchema relation in Relations)
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

        // Load Union Validation
        if (UnionValids is { Length: > 0 })
        {
            foreach (StructUnionValidation valid in UnionValids)
            {
                AnySchemaType? funcNode = await context.GetSchemaTypeAsync(valid.Func, preload: preload);
                if (funcNode is not FunctionType node)
                {
                    valid.Status = SchemaNodeStatus.StructHasWrongValid;
                    Status = SchemaNodeStatus.StructHasWrongValid;
                    continue;
                }
                valid.Status = null;
                valid.FuncNode = node;
                node.AddRef(this);
            }
        }
    }

    /// <inheritdoc />
    public override void Release()
    {
        foreach (StructFieldSchema config in Fields)
        {
            config.UnloadFieldSchema(this);
        }

        if (Relations != null)
        {
            foreach (StructRelationSchema relation in Relations)
            {
                relation.FuncNode?.RemoveRef(this);
                relation.FuncNode = null;
            }
        }

        if (UnionValids != null)
        {
            foreach (StructUnionValidation valid in UnionValids)
            {
                valid.FuncNode?.RemoveRef(this);
                valid.FuncNode = null;
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
        if (value is not JsonObject jObject)
            return (null, TYPE_VALUE_NOT_VALID);
        
        // validate fields
        StructTypeNode result = new(this);
        JsonObject? error = null;
        string? additionalField = null;
        foreach (StructFieldSchema field in Fields)
        {
            if (field.DisplayOnly ?? false) continue;
            if (field.SchemaType is null) continue;

            if (jObject.ContainsKey(field.Name) && !jObject[field.Name].IsEmpty())
            {
                (AnySchemaNode? v, JsonNode? e) = await field.SchemaType.ValidateValueAsync(context, jObject[field.Name]!);
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
            else if (field.Unpack ?? false)
            {
                additionalField = field.Name;
            }
            else if (field.Require ?? false)
            {
                StructRelationSchema? r = Relations?.FirstOrDefault(r => 
                    r.Field.Equals(field.Name, StringComparison.OrdinalIgnoreCase) &&
                    r.Prop.Equals(PROPERTY_DEFAULT, StringComparison.OrdinalIgnoreCase));

                // Complete by relation
                if (r != null)
                {
                    r.FuncNode ??=  await context.GetSchemaTypeAsync<FunctionType>(r.Func);
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
            string[] fieldsName = Fields.Select(f => f.Name).ToArray();
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
        if (error == null && UnionValids is { Length: > 0 })
        {
            foreach(var r in UnionValids)
            {
                var valid = r.FuncNode ?? await context.GetSchemaTypeAsync<FunctionType>(r.Func);
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
    public override bool CanBeUseAs(AnySchemaType other, bool exactly = false)
    {
        if (Name.Equals(NS_SYSTEM_STRUCT) || other.Name.Equals(NS_SYSTEM_STRUCT) || base.CanBeUseAs(other, exactly)) return true;
        if (other is not StructType @struct) return false;
        return @struct.Fields.Any(v => Fields.Any(f => f.Name.Equals(v.Name, StringComparison.OrdinalIgnoreCase))) 
               && @struct.Fields.All(v =>
               {
                   StructFieldSchema? match = Fields.FirstOrDefault(f => f.Name.Equals(v.Name, StringComparison.OrdinalIgnoreCase));
                   return match?.SchemaType == null ? !(v.Require ?? false) : v.SchemaType != null && match.SchemaType.CanBeUseAs(v.SchemaType);
               });
    }

    public override IEnumerable<AnySchemaType> GetDependNodes()
    {
        foreach (StructFieldSchema field in Fields)
        {
            if (field.SchemaType != null)
                yield return field.SchemaType;
        }
        if (Relations != null)
        {
            foreach (StructRelationSchema relation in Relations)
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
    public StructFieldSchema? GetField(string fieldName) 
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
        
        List<PropertyInfo> fieldMaps = [];
        string[] primarys = [];
        Dictionary<string, string[]> indexes = [];
        SchemaAttribute? typeAttr = type.GetCustomAttribute<SchemaAttribute>();
        string typeName = typeAttr?.Name ?? $"{(string.IsNullOrWhiteSpace(ns) ? "" : $"{ns}.")}{type.Name.ToLowerInvariant()}";
        bool hasNestType = false;

        // Keep in the same namespace if the struct is marked with SchemaAttribute, otherwise use the parent namespace
        if (typeAttr?.Name != null)
            ns = string.Join('.', typeAttr.Name.Split('.', StringSplitOptions.RemoveEmptyEntries).SkipLast(1));

        List<StructFieldSchema> fieldConfigs = [];
        foreach (PropertyInfo p in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(p =>
                     p.GetMethod?.IsPrivate != true &&
                     p.GetCustomAttribute<NotMappedAttribute>() == null &&
                     p is { CanRead: true, CanWrite: true })
                     .OrderBy(p => p.MetadataToken)) // with define order
        {

            SchemaAttribute? fieldAttr = p.GetCustomAttribute<SchemaAttribute>();
            string fieldName = p.Name.ToCamelCase();
            
            // Gets the field type
            string? fieldType = fieldAttr?.Name;
            if (string.IsNullOrWhiteSpace(fieldType))
            {
                var info = p.PropertyType.GetSchemaTypeInfo();
                if (info?.BaseType == type)
                {
                    hasNestType = true;
                    if (info.AnyArray)
                    {
                        fieldType = $"{typeName}s";
                    }
                    else
                        fieldType = typeName;
                }
                else
                    fieldType = info?.GetSchemaType(true, ns);
                if (string.IsNullOrWhiteSpace(fieldType))
                    continue;
            }
            
            StructFieldSchema config = new ()
            {
                Name = fieldName,
                Type = fieldType,
                Display = fieldAttr?.Display ?? type.GetSummaryFromXmlDoc(p) ?? $"{typeName}.{fieldName}",
            };

            if (p.GetCustomAttribute<RequiredAttribute>() != null)
            {
                config.Additional ??= [];
                config.Additional["require"] = JsonSerializer.SerializeToElement(true);
            }

            // limit check
            if (config.Type == NS_SYSTEM_STRING)
            {
                StringLengthAttribute? strLenAttr = p.GetCustomAttribute<StringLengthAttribute>();
                MaxLengthAttribute? maxLengthAttribute = p.GetCustomAttribute<MaxLengthAttribute>();

                long? upLimit = strLenAttr?.MaximumLength ?? maxLengthAttribute?.Length;
                long? lowLimit = strLenAttr?.MinimumLength;

                if (upLimit.HasValue)
                {
                    config.Additional ??= [];
                    config.Additional[PROPERTY_UPLIMIT] = JsonSerializer.SerializeToElement(upLimit.Value);
                }

                if (lowLimit.HasValue)
                {
                    config.Additional ??= [];
                    config.Additional[PROPERTY_LOWLIMIT] = JsonSerializer.SerializeToElement(lowLimit.Value);
                }
            }
            else
            {
                RangeAttribute? rangeAttr = p.GetCustomAttribute<RangeAttribute>();
                if (rangeAttr != null)
                {
                    config.Additional ??= [];
                    config.Additional[PROPERTY_UPLIMIT] = JsonSerializer.SerializeToElement(rangeAttr.Maximum);
                    config.Additional[PROPERTY_LOWLIMIT] = JsonSerializer.SerializeToElement(rangeAttr.Minimum);
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
            
            fieldMaps.Add(p);
            fieldConfigs.Add(config);
        }

        NodeSchema structSchema = new ()
        {
            Name = typeName,
            Type = SchemaType.Struct,
            Display = typeAttr?.Display ?? type.GetSummaryFromXmlDoc() ?? typeName,
            Struct = new StructSchema
            {
                Fields = fieldConfigs.ToArray(),
                Atomic = hasNestType ? true : null
            }
        };
        CsharpTypeProperties[structSchema.Name.ToLower()] = fieldMaps;

        if (SystemLocale.HasLocales)
        {
            SystemLocale.Translate(structSchema.Display, structSchema.Name);
            foreach (StructFieldSchema field in structSchema.Struct!.Fields)
                SystemLocale.Translate(field.Display);
        }

        if (primarys.Length == 0 && !hasNestType) return [structSchema];
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
                Atomic = hasNestType ? true : null,
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
            Fields = schema.Fields,
            Relations = schema.Relations,
            Atomic = schema.Atomic
        });
    }
    
    #endregion
    
    #region Generic Struct Implementations

    /// <summary>
    /// Get the generic struct type
    /// </summary>
    public async Task<StructType?> GetGenericTypeAsync(SchemaContext context, string[] types)
    {
        string[] generics = Fields.Where(f => f.SchemaType is GenericType).Select(f => f.Type).Distinct().ToArray();
        if (generics.Length == 0 || generics.Length != types.Length) return null; // Not a generic struct or not contains

        _genericTypes ??= new ConcurrentDictionary<string, StructType>();
        string key = string.Join('|', types);
        if (_genericTypes.TryGetValue(key, out StructType? type)) return type;
        
        // Generate new struct type
        StructType newStruct = new()
        {
            Name = $"{Name}<{string.Join(',', types)}>",
            Display = $"{Locale.LIST_PREFIX}{string.Join(",", types.Select(t => $"{{@{t}}}"))}{Locale.LIST_SUFFIX}",
            Fields = new StructFieldSchema[Fields.Length],
            Namespace = Namespace,
            Relations = Relations,
            Atomic = Atomic,
        };

        for (int i = 0; i < Fields.Length; i++)
        {
            StructFieldSchema f = Fields[i];
            
            if (f.SchemaType is GenericType)
            {
                StructFieldSchema copy = f.ToJsonNode()!.FromJson<StructFieldSchema>()!;
                int index = Array.IndexOf(generics, f.Type);
                copy.Type = types[index];
                AnySchemaType? schemaType = await context.GetSchemaTypeAsync(copy.Type);
                if (schemaType == null || schemaType.Type is SchemaType.Namespace or SchemaType.Func)
                {
                    return null;
                }
                copy.SchemaType = schemaType;
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
