using System.Collections.Concurrent;
using System.Reflection;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property;
using SchemaNode.Property.Schema;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using ValueType = SchemaNode.Property.Schema.ValueType;

namespace SchemaNode.Service;

/// <summary>
/// The handler to load schema kinds from assemblies by scanning [Meta&lt;AsSchemaKind&gt;] attributes
/// </summary>
internal sealed class NodeSchemaRuntimeBuilder : IStageHandler
{
    #region Schema Kinds
    
    private readonly ConcurrentDictionary<string, SchemaKindInfo> _schemaKinds = new();
    
    /// <summary>
    /// Represents a registered schema kind with its metadata
    /// </summary>
    record SchemaKindInfo(string Kind, Type SchemaType, Type? RuntimeType, Type? ValueType, int Order);

    /// <inheritdoc/>
    public void OnSchemaKindLoading(SchemaContext context, IEnumerable<Assembly> assemblies)
    {
        // Check if using Schema runtime
        SchemaRuntime? runtime = context.Runtime  as SchemaRuntime;
        if (runtime == null) return;

        foreach (Assembly assembly in assemblies)
        {
            foreach (Type type in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
            {
                // Check if this type has [Meta<AsSchemaKind>] attribute
                AsSchemaKind? asSchemaKind = type.GetMetaProperty<AsSchemaKind>();
                if (asSchemaKind == null) continue;

                string kind = asSchemaKind.Value ?? type.Name.GetSchemaKind();
                int order = asSchemaKind is IOrderProperty orderProp ? orderProp.Order : 0;

                // Get the runtime type mapping from [Meta<RuntimeType>]
                Type? runtimeType = type.GetMetaProperty<RuntimeType>()?.Value;
                
                // Get the value type mapping from [Meta<ValueType>]
                Type? valueType = type.GetMetaProperty<ValueType>()?.Value;

                // Reigster the schema kind
                _schemaKinds[kind] = new SchemaKindInfo(kind, schemaType, runtimeType, valueType, order);
                context.LogDebug("[SchemaKind] Registered kind '{Kind}' -> schema={SchemaType}, runtime={RuntimeType}, value={ValueType}, order={Order}",
                    kind, type.Name, runtimeType?.Name ?? "None", valueType?.Name ?? "None", order);
            }
        }
    }
    
    #endregion
    
    private static readonly ConcurrentBag<(Type PropertyType, string[] ForSchemas)> PendingProperties = [];

    /// <inheritdoc />
    public void OnPropertyLoading(SchemaContext context, IEnumerable<Assembly> assemblies)
    {
        foreach (Assembly assembly in assemblies)
        {
            foreach (Type type in assembly.GetTypes().Where(t =>
                         typeof(IProperty).IsAssignableFrom(t) &&
                         t is { IsClass: true, IsAbstract: false }))
            {
                ForSchema? forSchema = type.GetMetaProperty<ForSchema>();
                if (forSchema?.Value is { Length: > 0 })
                {
                    PendingProperties.Add((type, forSchema.Value));
                }
            }
        }
    }

    /// <inheritdoc />
    public void OnSchemaKindLoaded(SchemaContext context, IEnumerable<Assembly> assemblies)
    {
        ISchemaRuntime runtime = context.Runtime;

        // Now register all collected properties into the runtime
        foreach (var (propertyType, forSchemas) in PendingProperties)
        {
            runtime.RegisterSchemaProperty(propertyType, forSchemas);
            context.LogDebug("[SchemaProperty] Registered property '{PropertyType}' for schemas [{Schemas}]",
                propertyType.Name, string.Join(", ", forSchemas));
        }

        PendingProperties.Clear();
    }
    
    /// <inheritdoc />
    public void OnSystemSchemaLoading(SchemaContext context, IEnumerable<Assembly> assemblies)
    {
        ISchemaRuntime runtime = context.Runtime;
        var assemblyList = assemblies as Assembly[] ?? assemblies.ToArray();

        // Pre-scan: trigger all attribute constructions on all types (including abstract)
        // to collect RecordProperty values before enum schema generation
        Dictionary<string, Type> recordEnumMap = []; // schemaName → RecordProperty type
        foreach (Assembly assembly in assemblyList)
        {
            foreach (Type type in assembly.GetTypes())
            {
                // Trigger attribute constructors → RecordProperty.Record()
                _ = type.GetCustomAttributes(false);

                // Colleqct RecordProperty → enum schema mappings
                if (!type.IsAbstract && type.BaseType is { IsGenericType: true } bt
                    && bt.GetGenericTypeDefinition() == typeof(RecordProperty<>))
                {
                    Record? record = type.GetMetaProperty<Record>();
                    if (record?.Value != null)
                        recordEnumMap[record.Value] = type;
                }
            }
        }

        // Main scan: generate system schemas
        foreach (Assembly assembly in assemblyList)
        {
            foreach (Type type in assembly.GetTypes().Where(t => t is { IsAbstract: false }))
            {
                SchemaType? schemaTypeMeta = type.GetMetaProperty<SchemaType>();
                if (schemaTypeMeta?.Value == null) continue;

                string schemaName = schemaTypeMeta.Value;

                // Skip schema definition types themselves (ScalarSchema, EnumSchema, etc.)
                if (type.GetMetaProperty<AsSchemaKind>() != null) continue;

                // Generate based on what the type is
                NodeSchema[] schemas;

                if (type.IsEnum)
                {
                    schemas = GenerateEnumSchemas(type, schemaName, recordEnumMap);
                }
                else if (type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IScalarType<>)))
                {
                    schemas = GenerateScalarSchemas(type, schemaName);
                }
                else
                {
                    // Struct-like or other class types with [Meta<SchemaType>]
                    schemas = GenerateStructSchemas(type, schemaName);
                }

                foreach (NodeSchema schema in schemas)
                {
                    runtime.SaveSystemNodeSchema(schema);
                    context.LogDebug("[SystemSchema] Registered system schema '{Name}' kind='{Kind}'",
                        schema.Name, schema.Kind);
                }
            }
        }
    }

    #region Schema Generators

    /// <summary>
    /// Generate a scalar NodeSchema from a scalar type class
    /// </summary>
    static NodeSchema[] GenerateScalarSchemas(Type type, string schemaName)
    {
        NodeSchema schema = new()
        {
            Name = schemaName,
            Kind = nameof(ScalarSchema).GetSchemaKind(),
        };

        // Check for a base scalar type
        Type? baseType = type.BaseType;
        if (baseType != null && baseType != typeof(object))
        {
            SchemaType? baseMeta = baseType.GetMetaProperty<SchemaType>();
            if (baseMeta?.Value != null)
            {
                // Set the base type via SetProperty
                ScalarProperty prop = new();
                prop.SetValue(new ScalarSchema { Base = baseMeta.Value });
                schema.SetProperty(prop);
            }
        }

        // Collect constraint properties from meta attributes
        CollectConstraintExtensions(type, schema);

        return [schema];
    }

    /// <summary>
    /// Generate enum NodeSchema from a C# enum type.
    /// For empty enums with linked RecordProperty, enum values are dynamically collected.
    /// </summary>
    static NodeSchema[] GenerateEnumSchemas(Type type, string schemaName, Dictionary<string, Type> recordEnumMap)
    {
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
        EnumValueType valueType;
        EnumValueInfo[] values;

        if (fields.Length > 0)
        {
            // Non-empty C# enum: use field values
            valueType = type.GetCustomAttribute<FlagsAttribute>() != null
                ? EnumValueType.Flags
                : EnumValueType.String;

            values = fields.Select(f => new EnumValueInfo
            {
                Name = schemaName + "." + f.Name.ToLower(),
                Value = valueType switch
                {
                    EnumValueType.String => (f.GetCustomAttribute<EnumMemberAttribute>()?.Value ?? f.Name)
                        .ToCamelCase(),
                    _ => $"{f.GetValue(null)}"
                },
                HasSubList = false,
            }).ToArray();
        }
        else if (recordEnumMap.TryGetValue(schemaName, out Type? rpType))
        {
            // Empty enum with linked RecordProperty: collect dynamically recorded values
            valueType = EnumValueType.String;
            values = RecordPropertyExtensions.GetRecordedValues(rpType)
                .Where(p => p.HasValue)
                .Select(p =>
                {
                    string code = p.GetValue<string>() ?? "";
                    return new EnumValueInfo
                    {
                        Value = code,
                        Name = schemaName + "." + code,
                        HasSubList = false,
                    };
                }).ToArray();
        }
        else
        {
            // Empty enum without RecordProperty: generate empty enum schema
            valueType = EnumValueType.String;
            values = [];
        }

        NodeSchema schema = new()
        {
            Name = schemaName,
            Kind = nameof(EnumSchema).GetSchemaKind(),
        };

        EnumProperty prop = new();
        prop.SetValue(new EnumSchema
        {
            Type = valueType,
            Values = values,
        });
        schema.SetProperty(prop);

        return [schema];
    }

    /// <summary>
    /// Generate struct NodeSchema from a C# class type
    /// </summary>
    static NodeSchema[] GenerateStructSchemas(Type type, string schemaName)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .ToArray();

        if (properties.Length == 0) return [];

        StructFieldSchema[] fields = properties.Select(p =>
        {
            SchemaType? fieldType = p.GetMetaProperty<SchemaType>();
            string typeName = fieldType?.Value ?? GetDefaultSchemaType(p.PropertyType);

            return new StructFieldSchema
            {
                Name = p.Name.ToCamelCase(),
                Type = typeName,
            };
        }).ToArray();

        NodeSchema schema = new()
        {
            Name = schemaName,
            Kind = nameof(StructSchema).GetSchemaKind(),
        };

        StructProperty prop = new();
        prop.SetValue(new StructSchema
        {
            Fields = fields,
        });
        schema.SetProperty(prop);

        return [schema];
    }

    #endregion

    #region Utility

    /// <summary>
    /// Collect constraint extensions from meta attributes on the type and add them to the schema
    /// </summary>
    static void CollectConstraintExtensions(Type type, NodeSchema schema)
    {
        foreach (IPropertyAttribute attr in type.GetCustomAttributes(false).OfType<IPropertyAttribute>())
        {
            if (attr.Property is IConstraintProperty constraint && constraint.HasValue)
            {
                string propName = attr.Property.GetType().GetPropertyName();
                JsonNode? value = constraint.GetValue<JsonNode>();
                if (value != null)
                {
                    schema.Extensions ??= [];
                    schema.Extensions[propName] = value;
                }
            }
        }
    }

    /// <summary>
    /// Map a CLR type to a default schema type name
    /// </summary>
    static string GetDefaultSchemaType(Type type)
    {
        Type underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying == typeof(string)) return NS_SYSTEM_STRING;
        if (underlying == typeof(bool)) return NS_SYSTEM_BOOL;
        if (underlying == typeof(int) || underlying == typeof(long)) return NS_SYSTEM_INT;
        if (underlying == typeof(float)) return NS_SYSTEM_FLOAT;
        if (underlying == typeof(double)) return NS_SYSTEM_DOUBLE;
        if (underlying == typeof(decimal)) return NS_SYSTEM_NUMBER;
        if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset)) return NS_SYSTEM_FULL_DATE;
        if (underlying == typeof(Guid)) return NS_SYSTEM_GUID;

        // Check for schema type on the type itself
        SchemaType? meta = underlying.GetMetaProperty<SchemaType>();
        if (meta?.Value != null) return meta.Value;

        return NS_SYSTEM_STRING;
    }

    #endregion
    
    #region Inner type
    
    #endregion
}