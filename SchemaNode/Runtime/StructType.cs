using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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
public class StructType: AnySchemeType
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
            AnySchemeType? baseNode = await context.GetSchemaNodeAsync(Base, preload: preload);
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
            AnySchemeType? typeNode = await context.GetSchemaNodeAsync(field.Type, preload: preload);
            if (typeNode == null || typeNode.Type is SchemaType.Namespace or SchemaType.Func)
            {
                Status = SchemaNodeStatus.StructMemberWrongType;
                continue;
            }
            field.TypeNode = typeNode;
            typeNode.AddRef(this);
        }
        
        // Load Relation
        if (Relations != null)
        {
            foreach (StructFieldRelation relation in Relations)
            {
                AnySchemeType? funcNode = await context.GetSchemaNodeAsync(relation.Func, preload: preload);
                if (funcNode is not FunctionType node)
                {
                    Status = SchemaNodeStatus.StructRelationshipWrongFunc;
                    continue;
                }
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
            config.TypeNode?.RemoveRef(this);
            config.TypeNode = null;
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
        List<AnySchemeType> relTypes = [this];
        
        if (UsedBy is { Count: > 0 })
            relTypes.AddRange(UsedBy.Keys.Where(p => p.Type == SchemaType.Struct));
        foreach (AnySchemeType node in relTypes.ToList().Where(node => node.UsedBy is { Count: > 0 }))
            relTypes.AddRange(node.UsedBy!.Keys.Where(p => p.Type == SchemaType.Array));

        // Gets the relative field type
        foreach (AppFieldNode field in relTypes.Where(node => node.UsedByApp is { Count: > 0 }).SelectMany(node => node.UsedByApp!.Keys))
            field.Schema = null; // Clear to reload
    }

    /// <inheritdoc />
    public override async Task<(AnySchemaNode? value, JsonNode? error)> ValidateValueAsync(SchemaContext context, JsonNode value)
    {
        if (value is not JsonObject jobject)
            return (null, TYPE_VALUE_NOT_VALID);
        
        // validate fields
        StructNode result = new(this);
        JsonObject? error = null;
        foreach (StructFieldConfig field in Fields)
        {
            if (field.DisplayOnly ?? false) continue;
            if (field.TypeNode is null) continue;

            if (jobject.ContainsKey(field.Name) && !jobject[field.Name].IsEmpty())
            {
                (AnySchemaNode? v, JsonNode? e) = await field.TypeNode.ValidateValueAsync(context, jobject[field.Name]!);
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
    public override bool CanBeUseAs(AnySchemeType other)
    {
        if (this == other || Name.Equals(NS_SYSTEM_STRUCT) || other.Name.Equals(NS_SYSTEM_STRUCT)) return true;
        if (other is not StructType @struct) return false;
        StructType? baseNode = BaseNode;
        while (baseNode != null && baseNode != @struct) baseNode = baseNode.BaseNode;
        return baseNode == @struct || 
               @struct.Fields.Any(v => Fields.Any(f => f.Name.Equals(v.Name, StringComparison.OrdinalIgnoreCase))) 
               && @struct.Fields.All(v =>
               {
                   StructFieldConfig? match = Fields.FirstOrDefault(f => f.Name.Equals(v.Name, StringComparison.OrdinalIgnoreCase));
                   return match?.TypeNode == null ? !(v.Require ?? false) : v.TypeNode != null && match.TypeNode.CanBeUseAs(v.TypeNode);
               });
    }

    public override IEnumerable<AnySchemeType> GetDependNodes()
    {
        if (BaseNode != null) yield return BaseNode;
        foreach (StructFieldConfig field in Fields)
        {
            if (field.TypeNode != null)
                yield return field.TypeNode;
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

    #endregion
    
    #region Static Feature

    /// <summary>
    /// Generate system enum
    /// </summary>
    public static NodeSchema[] GenerateSystemStruct(Type type, string? ns = null)
    {
        SchemaStructAttribute? attr = type.GetCustomAttribute<SchemaStructAttribute>();
        if (type is { IsClass: false, IsValueType: false } || 
            type is { IsClass: true, IsAbstract: true } ||
            (type.IsValueType && type.IsPrimitiveLike())) return [];
        
        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => 
            p.GetCustomAttribute<SchemaStructMemIgnoreAttribute>() == null &&
            p is { CanRead: true, CanWrite: true } && 
            !string.IsNullOrWhiteSpace(p.PropertyType.GetSchemaType(true))
        ).ToArray();
        if (properties.Length == 0) return [];

        NodeSchema structSchema = new NodeSchema
        {
            Name = $"{(string.IsNullOrWhiteSpace(ns) ? "" : $"{ns}.")}{(attr?.Type ?? type.Name).ToLowerInvariant()}",
            Type = SchemaType.Struct,
            Display = attr?.Display ?? type.Name,
            Struct = new StructSchema
            {
                Fields = properties.Select(p =>
                {
                    SchemaStructMemAttribute? memAttr = p.GetCustomAttribute<SchemaStructMemAttribute>();
                    return new StructFieldConfig
                    {
                        Name = p.Name,
                        Type = p.PropertyType.GetSchemaType()!,
                        Require = p.GetCustomAttribute<RequiredMemberAttribute>() != null,
                        Display = memAttr?.Display ?? p.Name,
                        Desc = memAttr?.Desc,
                    };
                }).ToArray()
            }
        };
        
        if (attr?.Primary == null) return [structSchema];
        NodeSchema arraySchema = new NodeSchema
        {
            Name = $"{structSchema.Name}s",
            Type = SchemaType.Array,
            Display = $"[Array]{structSchema.Display.Key}",
            Array = new ArraySchema
            {
                Element = structSchema.Name,
                Primary = attr.Primary.Where(p => structSchema.Struct.Fields.Any(f => f.Name.Equals(p, StringComparison.OrdinalIgnoreCase))).ToArray()
            }
        };
        return [structSchema, arraySchema];
    }

    #endregion
    
    #region Conversion
    
    /// <summary>
    /// Convert the node to schema
    /// </summary>
    public static implicit operator NodeSchema?(StructType? schema)
    {
        if (schema == null) return null;
        return new NodeSchema
        {
            Name = schema.Name,
            Type = schema.Type,
            Display = schema.Display,
            LoadState = schema.LoadState,
            Struct = new StructSchema
            {
                Base = schema.Base,
                Relations = schema.Relations,
                Fields = schema.Fields,
                Additional = schema.Additional,
            }
        };
    }
    
    #endregion
}
