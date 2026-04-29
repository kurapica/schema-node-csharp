using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property;
using SchemaNode.Property.Record;
using SchemaNode.Property.Schema;
using SchemaNode.Runtime;
using SchemaNode.Scalar;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using NodeType = SchemaNode.Property.Schema.NodeType;
using SchemaType = SchemaNode.Property.Schema.SchemaType;

namespace SchemaNode.Service;

/// <summary>
/// The schema generator used to convert C# features into node schemas
/// </summary>
public interface INodeSchemaGenerator
{
    /// <summary>
    /// Generate the node schemas from type
    /// </summary>
    IEnumerable<NodeSchema> GenerateSchema(SchemaRuntime runtime, Type type, string @namespace, string name, Func<Type, string, string?> typeResolver);
} 

/// <summary>
/// The handler to load system schema kinds from assemblies
/// </summary>
internal sealed class NodeRuntimeStageHandler : IRuntimeStageHandler
{
    /// <inheritdoc />
    public async Task OnSystemSchemaLoading(ISchemaContext context, IEnumerable<Assembly> assemblies)
    {
        if (context is not SchemaContext schemaContext || context.Runtime is not SchemaRuntime runtime) return; // not support

        #region Prepare

        List<(string kind, Type schemaType, Type? propertyType)> nodeSchemaTypes = [];
        List<INodeSchemaGenerator> schemaGenerators = [];
        Dictionary<string, INodeSchemaGenerator> kindGenerators = [];
        Dictionary<string, string> arrayTypes = [];
        
        // Gets all node schema kinds & property type & generators
        foreach (var (kind, schemaType) in runtime.GetSchemaKinds())
        {
            // Gets node schema kind
            if (schemaType.GetMetaProperty<NodeSchemaKind>() is not { HasValue: true } schemaKind) continue;
            
            // Register node types
            if (schemaType.GetMetaProperty<NodeType>()?.Value is { } runtimeType)
                runtime.RegisterNodeType(schemaKind.Value!, runtimeType);
            
            // Gets the match node schema property type
            nodeSchemaTypes.Add((kind, schemaType,
    runtime.GetSchemaKindProperties(SCHEMA_KIND_NODE).
                FirstOrDefault(p => p.GetGenericBaseType(typeof(Property<>))?.
                GetGenericArguments().ElementAtOrDefault(0) == schemaType)));
            
            // Load schema generators
            if (schemaType.GetMetaProperty<SchemaGenerator>()?.Value is { } schemaGenerator)
            {
                var generator = schemaGenerators.FirstOrDefault(g => g.GetType() == schemaGenerator);
                if (generator is null)
                {
                    generator = ActivatorUtilities.CreateInstance<INodeSchemaGenerator>(context.Services, schemaGenerator);
                    schemaGenerators.Add(generator);
                }
                kindGenerators[kind] = generator;
            }
        }
        
        #endregion
        
        #region System Schema

        #region Special

        // system.array
        {
            NodeSchema schema = NodeSchema.Create(nameof(ArraySchema), NS_SYSTEM_ARRAY, typeof(List<object>));
            schema.SetProperty<ArrayProperty, ArraySchema>(new ArraySchema{ Element = NS_SYSTEM_OBJECT });
            runtime.SaveSystemSchema(schema);
            arrayTypes[NS_SYSTEM_OBJECT] = NS_SYSTEM_ARRAY;
        }

        // system.list<T>
        {
            NodeSchema schema = NodeSchema.Create(nameof(ArraySchema), NS_SYSTEM_LIST, typeof(List<>));
            ArraySchema arraySchema = new ArraySchema{ Element = NS_GENERIC_TYPE };
            arraySchema.SetProperty<Generics, GenericParameter[]>([new GenericParameter{ Name = NS_GENERIC_TYPE }]);
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

            // Check if need create the namespace schema manually
            IProperty[] props = assembly.GetMetaPropertiesForSchema<IProperty>(SCHEMA_KIND_NAMESPACE).ToArray();
            if (props.Length > 0)
            {
                NodeSchema nsSchema = NodeSchema.Create(SCHEMA_KIND_NAMESPACE, defaultNs);
                foreach (IProperty prop in props) nsSchema.SetProperty(prop);
                runtime.SaveSystemSchema(nsSchema);
            }
            
            HashSet<Type> handled = [];
            
            // scalar type first because the schema type is not the type declare it
            foreach (Type type in assembly.GetTypes().Where(t => t.IsSubclassOfGenericType(typeof(IScalarType<>))))
            {
                handled.Add(type);
                ResolveScalarSchema(type, defaultNs);
            }

            // other types
            foreach (Type type in assembly.GetTypes().Where(t => !handled.Contains(t)))
            {
                if (type.GetMetaProperty<SchemaType>() == null) continue;
                string _ = ResolveOtherSchema(type, defaultNs) ?? throw  new Exception($"Failed to resolve schema for type '{type}'");
            }
        }
        
        #endregion

        #endregion
        
        #region System Node Type Loading
        
        // Loading the system schema as node types, so they should be ready for other stages
        schemaContext.SystemMode = true;
        await LoadAllNodeTypes(string.Empty);
        
        #endregion
        
        return;

        #region Utility

        string ResolveScalarSchema(Type type, string defaultNs)
        {
            string? name = runtime.GetTypeSchema(type);
            if  (!string.IsNullOrWhiteSpace(name)) return name;

            SchemaType? schemaType = type.GetMetaProperty<SchemaType>();
            name = schemaType?.Value ?? $"{defaultNs}.{type.Name}".ToLowerInvariant();

            // node schema
            NodeSchema schema = NodeSchema.Create(nameof(ScalarSchema), name, 
                type.GetGenericBaseType(typeof(IScalarType<>))?.GetGenericArguments().ElementAtOrDefault(0));
            
            // gets the equivalents
            schema.Equivalents = type.GetMetaProperties<ClrEquivalent>()
                .Where(p => p.HasValue)
                .Select(p => p.Value!).ToArray();
            
            // scalar schema
            ScalarSchema scalarSchema = new ScalarSchema();
            
            // inherit the base type
            if (type.BaseType is { } superType)
                scalarSchema.Base = ResolveScalarSchema(superType, defaultNs);
            else if(schema.Type == null)
                throw new Exception($"Failed to get generic arguments for type '{type}'");
            
            // register scalar schema
            schema.SetProperty<ScalarProperty, ScalarSchema>(scalarSchema);
            runtime.SaveSystemSchema(ExtendSchema(schema, type));
            return name;
        }
        
        string? ResolveOtherSchema(Type type, string defaultNs)
        {
            TypeDetails? details = type.GetTypeDetails();

            // Special to handle array
            type = details?.BaseType ?? throw new Exception($"Failed to get generic arguments for type '{type}'");
            bool isArray = details.AnyArray;

            string? fullName = runtime.GetTypeSchema(type);
            if (!string.IsNullOrWhiteSpace(fullName)) return GetResult(fullName);

            SchemaType? schemaType = type.GetMetaProperty<SchemaType>();
            defaultNs = schemaType?.Value != null ? schemaType.Value.GetNamespace() : defaultNs;
            string name = schemaType?.Value?.GetSchemaName() ?? type.Name.ToLowerInvariant();
            
            OfSchema? ofSchema = type.GetMetaProperty<OfSchema>();
            NodeSchema? mainSchema = null;
            foreach (INodeSchemaGenerator generator in ofSchema is { HasValue: true } 
                 ? kindGenerators.Where(g => 
                         ofSchema.Value.Contains(g.Key, StringComparer.OrdinalIgnoreCase))
                     .Select(g => g.Value)
                 : schemaGenerators)
            {
                foreach (NodeSchema schema in generator.GenerateSchema(runtime, type, defaultNs, name, ResolveOtherSchema))
                {
                    runtime.SaveSystemSchema(schema.Type == type ? ExtendSchema(schema, type) : schema);
                    
                    // special for array
                    if (schema.Kind == SCHEMA_KIND_ARRAY && schema.GetProperty<ArrayProperty>()?.Value is {} arraySchema)
                        arrayTypes[arraySchema.Element] = schema.FullName;
                    
                    if (schema.Type == type) mainSchema = schema;
                }

            }

            return mainSchema != null ? GetResult(mainSchema.FullName) : null;

            string GetResult(string schemaName) => isArray
                ? (arrayTypes.TryGetValue(schemaName, out string? arraySchema)
                    ? arraySchema
                    : $"{NS_SYSTEM_LIST}<{schemaName}>")
                : schemaName;
        }

        // Save properties to the schema
        NodeSchema ExtendSchema(NodeSchema nodeSchema, Type type)
        {
            (string kind, Type schemaType, Type? propertyType)? info = nodeSchemaTypes.
                FirstOrDefault(t => nodeSchema.Kind.Equals(t.kind, StringComparison.OrdinalIgnoreCase));
            if (info?.propertyType == null) return nodeSchema;
            
            // get the property
            IProperty? property = nodeSchema.GetProperty(info.Value.propertyType);
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
        
        // mark all node types not loaded, so they can combine custom settings, like 'display' or custom properties
        schemaContext.SystemMode = false; // avoid system mode
        runtime.RootNamespace.ResetLoadState();
        return Task.CompletedTask;
    }
}