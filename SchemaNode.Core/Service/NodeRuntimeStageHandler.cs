using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Record;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Struct;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using NamespaceType = SchemaNode.Runtime.NamespaceType;
using NodeType = SchemaNode.Property.Core.NodeType;
using SchemaType = SchemaNode.Property.Core.SchemaType;

namespace SchemaNode.Service;

/// <summary>
/// The schema generator used to convert C# features into node schemas
/// </summary>
public interface INodeSchemaGenerator
{ /// <summary>
    /// Generate the node schemas from type, if typeResolver not provided, that means only the current type is allowed to be resolved,
    /// and all other dependent types should be ignored, so all system schema can be solved in the next schema generate stage.
    /// </summary>
    /// <param name="runtime">The schema runtime</param>
    /// <param name="type">The type to generate the schemas</param>
    /// <param name="namespace">The default namespace</param>
    /// <param name="name">The suggest schema name</param>
    /// <param name="typeResolver">The function used to solve the schema type of the given type</param>
    /// <returns>The node schemas that generated</returns>
    IEnumerable<NodeSchema> GenerateSchema(SchemaRuntime runtime, Type type, string @namespace, string name, Func<Type, string, Type[]?, string?>? typeResolver = null);
}

/// <summary>
/// The handler to load system schema kinds from assemblies
/// </summary>
internal sealed class NodeRuntimeStageHandler : IRuntimeStageHandler
{
    /// <inheritdoc />
    public void OnServiceInitialization(IServiceProvider provider, IServiceCollection services, IEnumerable<Assembly> assemblies)
    {
        #region Expression

        services.AddSingleton<IExpVisitor, IntrinsicExpVisitor>();
        services.AddSingleton<IExpVisitor, ArithmeticExpVisitor>();
        services.AddSingleton<IExpVisitor, LogicExpVisitor>();
        services.AddSingleton<IExpVisitor, CollectionExpVisitor>();

        #endregion
    }

    /// <inheritdoc />
    public void OnServiceInitialized(IServiceProvider provider, IServiceCollection services, IEnumerable<Assembly> assemblies)
    {
        // context item scan
        List<Type> itemProviders = [];
        foreach(ServiceDescriptor desc in services)
        {
            Type providerType = desc.ServiceType;
            if (providerType.GetInterfaces().FirstOrDefault(i => i.IsSubclassOfGenericType(typeof(ISchemaContextItemProvider<>))) is not null)
                itemProviders.Add(providerType);
        }
        
        // register for later consume
        services.AddSingleton(new SchemaContextItemProvider(itemProviders.ToArray()));
    }

    /// <inheritdoc />
    public async Task OnSystemSchemaLoading(ISchemaContext context, IEnumerable<Assembly> assemblies)
    {
        if (context is not SchemaContext schemaContext || context.Runtime is not SchemaRuntime runtime) return;

        #region Prepare

        List<(string kind, Type schemaType, Type? nodeSchemaProp)> nodeSchemaTypes = [];
        List<INodeSchemaGenerator> schemaGenerators = [];
        Dictionary<string, INodeSchemaGenerator> kindGenerators = [];
        Dictionary<Type, string> scalarTypes = [];
        
        // Gets all node schema kinds & property type & generators
        foreach ((string kind, Type type) in runtime.GetSchemaKinds())
        {
            // Gets node schema kind
            if (type.GetMetaProperty<NodeSchemaKind>() is not { HasValue: true } schemaKind) continue;
            
            // Register node types
            if (type.GetMetaProperty<NodeType>()?.Value is { } runtimeType)
                runtime.RegisterNodeType(schemaKind.Value!, runtimeType);
                        
            // Gets the match node schema property type
            nodeSchemaTypes.Add((kind, type,
    runtime.GetSchemaKindProperties(SCHEMA_KIND_NODE).
                FirstOrDefault(p => p.GetGenericBaseType(typeof(Property<>))?.
                GetGenericArguments().ElementAtOrDefault(0) == type)));
            
            // Load schema generators
            if (type.GetMetaProperty<SchemaGenerator>()?.Value is { } schemaGenerator)
            {
                var generator = schemaGenerators.FirstOrDefault(g => g.GetType() == schemaGenerator);
                if (generator is null)
                {
                    try
                    {
                        generator = (INodeSchemaGenerator)Activator.CreateInstance(schemaGenerator, nonPublic: true)!;
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Failed to create generator '{schemaGenerator.FullName}' for schema kind '{kind}' (schema type: {type.FullName}). IsAbstract={schemaGenerator.IsAbstract}", ex);
                    }
                    schemaGenerators.Add(generator);
                }
                kindGenerators[kind] = generator;
            }
        }
        
        #endregion
        
        #region System Schema

        #region Array

        // system.array
        {
            NodeSchema schema = NodeSchema.Create(SCHEMA_KIND_ARRAY, NS_SYSTEM_ARRAY, typeof(ArrayNode));
            schema.SetProperty<ArrayProperty, ArraySchema>(new ArraySchema{ Element = NS_SYSTEM_OBJECT });
            runtime.SaveSystemSchema(schema);
        }

        // system.list<T>
        {
            NodeSchema schema = NodeSchema.Create(SCHEMA_KIND_ARRAY, NS_SYSTEM_LIST, typeof(List<>));
            ArraySchema arraySchema = new ArraySchema{ Element = NS_GENERIC_TYPE };
            arraySchema.SetProperty<Generics, GenericParameter[]>([new GenericParameter(NS_GENERIC_TYPE)]);
            schema.SetProperty<ArrayProperty, ArraySchema>(arraySchema);
            runtime.SaveSystemSchema(schema);
        }

        #endregion
        
        #region Auto Scan

        // Scan and register system schemas
        foreach (Assembly assembly in assemblies)
        {
            string defaultNs = assembly.GetMetaProperty<SchemaType>()?.Value 
                               ?? assembly.GetName().Name?.ToLowerInvariant() 
                               ?? throw new Exception($"Failed to get default namespace for assembly '{assembly.FullName}'");

            // Check if we need create the namespace schema manually
            IProperty[] props = assembly.GetMetaPropertiesForSchema<IProperty>(SCHEMA_KIND_NAMESPACE).ToArray();
            if (props.Length > 0)
            {
                NodeSchema nsSchema = NodeSchema.Create(SCHEMA_KIND_NAMESPACE, defaultNs);
                foreach (IProperty prop in props) nsSchema.SetProperty(prop);
                runtime.SaveSystemSchema(nsSchema);
            }
            
            HashSet<Type> schemaTypes = [];
            
            // scalar type first because the schema type is not the type declare it
            foreach (Type type in assembly.GetTypes())
            {
                if (type.GetMetaProperty<SchemaType>() == null) continue;
                
                if (type.IsSubclassOfGenericType(typeof(IScalarType<>)))
                    ResolveScalarSchema(type, defaultNs);
                else
                    schemaTypes.Add(type);
            }
            
            // pre-generate
            foreach (Type type in schemaTypes)
                GenerateSchema(type, defaultNs, true);

            // generate
            foreach (Type type in schemaTypes)
                GenerateSchema(type, defaultNs);
        }
        
        #endregion
        
        #region System Context Items

        // system.context
        List<StructFieldSchema> fieldTypes = [];
        SchemaContextItemProvider provider = schemaContext.GetRequiredService<SchemaContextItemProvider>();
        foreach (Type providerType in provider.Providers)
        {
            Type? itemType = providerType.GetInterfaces()
                .FirstOrDefault(i =>  i.IsSubclassOfGenericType(typeof(ISchemaContextItemProvider<>)))?
                .GetGenericArguments().FirstOrDefault();
            if (itemType is null) continue;
            Assembly assembly = providerType.Assembly;
            string? schemaType = ResolveOtherSchema(itemType, assembly.GetMetaProperty<SchemaType>()?.Value 
                                                              ?? assembly.GetName().Name?.ToLowerInvariant() 
                                                              ?? throw new Exception($"Failed to get default namespace for assembly '{assembly.FullName}'"));
            if (string.IsNullOrEmpty(schemaType)) continue;

            // use the last part as field name
            StructFieldSchema field = new StructFieldSchema
            {
                Name = schemaType.GetSchemaName(),
                Type = schemaType
            };
            field.SetProperty<Display, LocaleString>($"{{@{schemaType}}}");
            fieldTypes.Add(field);
            
            // cache
            provider.BindSchemaContextItemProvider(field.Name, schemaType, providerType, itemType);
        }
        NodeSchema contextSchema = NodeSchema.Create(SCHEMA_KIND_STRUCT, NS_SYSTEM_CONTEXT);
        contextSchema.SetProperty<StructProperty, StructSchema>(new StructSchema { Fields = fieldTypes.ToArray() });
        runtime.SaveSystemSchema(contextSchema);
        
        #endregion

        #endregion
        
        #region System Node Type Loading
        
        // Loading the system schema as node types, so they should be ready for other stages
        schemaContext.SystemMode = true;
        
        // Loading system access
        SystemAccess access = schemaContext.System;
        access.Bool = (await schemaContext.GetNodeTypeAsync<Runtime.BoolType>(NS_SYSTEM_BOOL))!;
        access.String = (await schemaContext.GetNodeTypeAsync<Runtime.StringType>(NS_SYSTEM_STRING))!;
        access.Decimal = (await schemaContext.GetNodeTypeAsync<Runtime.DecimalType>(NS_SYSTEM_NUMBER))!;
        access.Int = (await schemaContext.GetNodeTypeAsync<Runtime.IntType>(NS_SYSTEM_INT))!;
        access.Date = (await schemaContext.GetNodeTypeAsync<Runtime.DateType>(NS_SYSTEM_DATE))!;
        access.Context = (await schemaContext.GetNodeTypeAsync<Runtime.StructType>(NS_SYSTEM_CONTEXT))!;
        
        // Loading all system node types
        await LoadAllNodeTypes(string.Empty);
        
        #endregion
        
        return;

        #region Utility

        string ResolveScalarSchema(Type type, string defaultNs)
        {
            // Already resolved via CLR generic-type key?
            if (scalarTypes.TryGetValue(type, out string? typeName)) return typeName;

            SchemaType? schemaType = type.GetMetaProperty<SchemaType>();
            string name = schemaType?.Value ?? $"{defaultNs}.{type.Name}".ToLowerInvariant();

            NodeSchema? schema;
            NodeSchema? baseSchema = null;
            Type? valType = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IScalarType<>))
                .Select(i => i.GetGenericArguments().FirstOrDefault())
                .LastOrDefault(t => t != null && runtime.GetTypeSchema(t) == null);
            
            // OfSchema marks a kind root — check it first so that types like Int (which extend Number
            // but belong to a different kind) are not incorrectly categorized by their C# base class.
            if (type.GetMetaProperty<OfSchema>() is { Value: { Length: > 0 } } ofSchema && valType != null)
            {
                schema = NodeSchema.Create(ofSchema.Value[0], name, valType);
            }
            else if (type.BaseType?.IsSubclassOfGenericType(typeof(IScalarType<>)) == true)
            {
                // Only follow scalar inheritance — ignore System.Object or other non-scalar bases.
                string baseTypeName = ResolveScalarSchema(type.BaseType, defaultNs);
                baseSchema = runtime.GetSystemSchema(baseTypeName) 
                             ?? throw new Exception($"Failed to resolve schema for type '{baseTypeName}'");
                schema = NodeSchema.Create(baseSchema.Kind, name, valType ?? baseSchema.Type);
            }
            else
            {
                throw new Exception($"Failed to resolve schema for type '{type}'");
            }
            
            // record
            scalarTypes[type] = name;
            
            // Gets the equivalents
            schema.Equivalents = type.GetMetaProperties<ClrEquivalent>()
                .Where(p => p.HasValue)
                .Select(p => p.Value!)
                .Append(type)
                .ToArray();
            
            // Gen scalar definitions
            Type? propType = nodeSchemaTypes.FirstOrDefault(t => t.kind.Equals(schema.Kind, StringComparison.OrdinalIgnoreCase)).nodeSchemaProp;
            if (propType != null)
            {
                IProperty prop = (ActivatorUtilities.CreateInstance(context.Services, propType) as IProperty)!;
                object? scalarSchema = Activator.CreateInstance(prop.Type);
                if (scalarSchema is ScalarSchema scalar && baseSchema != null)
                    scalar.Base = baseSchema.FullName;
                prop.SetValue(scalarSchema);
                schema.SetProperty(prop);
            }

            runtime.SaveSystemSchema(ExtendSchema(schema, type));
            return name;
        }

        string? GenerateSchema(Type type, string defaultNs, bool preGenerate = false)
        {
            SchemaType? schemaType = type.GetMetaProperty<SchemaType>();
            defaultNs = schemaType?.Value?.GetNamespace() ?? defaultNs;
            string name = schemaType?.Value?.GetSchemaName() ?? type.Name.ToLowerInvariant();
            
            OfSchema? ofSchema = type.GetMetaProperty<OfSchema>();
            NodeSchema? mainSchema = null;
            foreach (INodeSchemaGenerator generator in ofSchema is { HasValue: true } 
                         ? kindGenerators
                             .Where(g => ofSchema.Value.Contains(g.Key, StringComparer.OrdinalIgnoreCase))
                             .Select(g => g.Value)
                         : schemaGenerators)
            {
                foreach (NodeSchema schema in generator.GenerateSchema(runtime, type, defaultNs, name, preGenerate ? null : ResolveOtherSchema))
                {
                    runtime.SaveSystemSchema(!preGenerate && schema.Type == type ? ExtendSchema(schema, type) : schema);
                    if (schema.Type == type) mainSchema = schema;
                }
            }

            // System schemas must be explicitly resolvable at startup. A type with SchemaType
            // that no generator can handle is a configuration error that must not be ignored.
            if (mainSchema == null && schemaType != null)
                throw new Exception($"Failed to generate schema for type '{type.FullName}' with SchemaType '{schemaType.Value}'");
            return mainSchema?.FullName;
        }
        
        string? ResolveOtherSchema(Type type, string defaultNs, Type[]? genericArguments = null)
        {
            TypeDetail detail = type.GetTypeDetail();
            bool isArray = detail.AnyArray;

            // Check the core type
            type = detail.CoreType;

            if (detail.IsGenericParameter)
            {
                return genericArguments is { Length: > 0 } && Array.FindIndex(genericArguments, t => t == type) is { } index and >= 0
                    ? genericArguments.Length > 1 ? $"{NS_GENERIC_TYPE}{index}" : NS_GENERIC_TYPE
                    : null;
            }
            if (detail.IsGenericType)
            {
                string? genericTypeName = ResolveOtherSchema(type.GetGenericTypeDefinition(), defaultNs, genericArguments);
                if (string.IsNullOrWhiteSpace(genericTypeName)) return null;

                // Resolve generic arguments
                Type[] args = type.GetGenericArguments();
                string[] genericArgs = new string[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    string? n = ResolveOtherSchema(args[i], defaultNs, genericArguments);
                    if (string.IsNullOrWhiteSpace(n)) return null;
                    genericArgs[i] = n;
                }
                return GetResult($"{genericTypeName}<{string.Join(",", genericArgs)}>");
            }
            return GetResult(runtime.GetTypeSchema(type) ?? GenerateSchema(type, defaultNs));

            string? GetResult(string? name) => isArray && !string.IsNullOrWhiteSpace(name) ? runtime.GetSystemArraySchema(name) : name;
        }

        // Save properties to the schema
        NodeSchema ExtendSchema(NodeSchema nodeSchema, Type type)
        {
            (string kind, Type schemaType, Type? nodeSchemaProp)? info = nodeSchemaTypes.
                FirstOrDefault(t => nodeSchema.Kind.Equals(t.kind, StringComparison.OrdinalIgnoreCase));
            if (info?.nodeSchemaProp == null) return nodeSchema;
            
            // get the property
            IProperty? property = nodeSchema.GetProperty(info.Value.nodeSchemaProp);
            if (property == null) return nodeSchema;
            
            ExtensibleSchema? schema = property.GetValue<ExtensibleSchema>();
            if (schema == null) return nodeSchema;
            
            foreach (IProperty prop in type.GetMetaPropertiesForSchema<IProperty>(nodeSchema.Kind))
                schema.SetProperty(prop);
            
            // save back
            property.SetValue(schema);
            nodeSchema.SetProperty(property);
            return nodeSchema;
        }

        async Task LoadAllNodeTypes(string fullName)
        {
            Runtime.NodeType? nodeType = await schemaContext.GetNodeTypeAsync(fullName);
            if (nodeType is not NamespaceType ns) return;
            
            foreach (NodeSchema schema in ns.GetNodeSchemas())
                await LoadAllNodeTypes(schema.FullName);
        }
        
        #endregion
    }

    /// <inheritdoc />
    public Task OnSchemaLoadingAsync(ISchemaContext context)
    {
        if (context is not SchemaContext schemaContext || context.Runtime is not SchemaRuntime runtime) return Task.CompletedTask; // not support
        
        // mark all node types not loaded, so they can combine custom schemas
        schemaContext.SystemMode = false; // avoid system mode
        runtime.RootNamespace.ResetLoadState();
        
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnActivatedAsync(ISchemaContext context)
    {
        // clear cache
        TypeDetailExtensions.ClearTypeDetailCache();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnDeactivatedAsync(ISchemaContext context)
    {
        // clear cache
        TypeDetailExtensions.ClearTypeDetailCache();
        return Task.CompletedTask;
    }
}