using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Generator;
using SchemaNode.Property;
using SchemaNode.Property.Presentation;
using SchemaNode.Property.Schema;
using SchemaNode.Runtime;
using SchemaNode.Scalar;
using SchemaNode.Schema;
using SchemaNode.Struct;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Service;

/// <summary>
/// The handler to load schema kinds from assemblies by scanning [Meta&lt;AsSchemaKind&gt;] attributes
/// </summary>
internal sealed class NodeRuntimeStageHandler : IRuntimeStageHandler
{
    /// <inheritdoc />
    public async Task OnSystemSchemaLoading(ISchemaContext context, IEnumerable<Assembly> assemblies)
    {
        SchemaRuntime? runtime = context.Runtime as SchemaRuntime;
        if (runtime is null) return; // not support

        #region Prepare

        List<(string kind, Type nodeSchemaType, Type? propertyType)> nodeSchemaTypes = [];
        List<INodeSchemaGenerator> schemaGenerators = [];
        
        // Gets all node schema kinds & property type & generators
        foreach (var (kind, schemaType) in runtime.GetSchemaKinds())
        {
            // Gets node schema types
            if (schemaType.GetMetaProperty<NodeSchemaType>()?.Value is not { } nodeSchemaType) continue;
            
            // Gets the match node schema property type
            nodeSchemaTypes.Add((kind, nodeSchemaType, 
    runtime.GetSchemaKindProperties(nameof(NodeSchema).GetSchemaKind()).
                FirstOrDefault(p => p.GetGenericBaseType(typeof(Property<>))?.
                    GetGenericArguments().ElementAtOrDefault(0) == nodeSchemaType)));
            
            // Load schema generators
            if (schemaType.GetMetaProperty<SchemaGenerator>()?.Value is { } schemaGenerator&&
                schemaGenerators.All(g => g.GetType() != schemaGenerator))
                schemaGenerators.Add(ActivatorUtilities.CreateInstance<INodeSchemaGenerator>(context.Services, schemaGenerator));
        }
        
        #endregion
        
        #region Register
        
        #region Special types

        #region system.schema.node.kind

        NodeSchema nodeKinds = new NodeSchema
        {
            Name = NS_SYSTEM_SCHEMA_NODE_KIND.GetSchemaName(),
            Namespace = NS_SYSTEM_SCHEMA_NODE_KIND.GetNamespace(),
            Kind = nameof(EnumSchema).GetSchemaKind(),
            Type = typeof(NodeSchemaKind)
        };
        nodeKinds.SetProperty<Display, LocaleString>(NS_SYSTEM_SCHEMA_NODE_KIND);
        nodeKinds.SetProperty<EnumProperty, EnumSchema>(new EnumSchema
        {
            Type = EnumValueType.String,
            Values = nodeSchemaTypes.Select(t => new EnumValueInfo
            {
                Name = $"{NS_SYSTEM_SCHEMA_NODE_KIND}.{t.kind}",
                Value = t.kind,
            }).ToArray()
        });
        runtime.SaveSystemSchema(nodeKinds);

        #endregion

        #region system.schema.node.valuekind

        NodeSchema valueKinds = new NodeSchema
        {
            Name = NS_SYSTEM_SCHEMA_NODE_VALUE_KIND.GetSchemaName(),
            Namespace = NS_SYSTEM_SCHEMA_NODE_VALUE_KIND.GetNamespace(),
            Kind = nameof(EnumSchema).GetSchemaKind(),
            Type = typeof(ValueSchemaKind)
        };
        valueKinds.SetProperty<Display, LocaleString>(NS_SYSTEM_SCHEMA_NODE_VALUE_KIND);
        valueKinds.SetProperty<EnumProperty, EnumSchema>(new EnumSchema
        {
            Type = EnumValueType.String,
            Values = nodeSchemaTypes
                .Where(t => t.nodeSchemaType.IsAssignableTo(typeof(ValueSchemaType)))
                .Select(t => new EnumValueInfo
            {
                Name = $"{NS_SYSTEM_SCHEMA_NODE_KIND}.{t.kind}",
                Value = t.kind,
            }).ToArray()
        });
        runtime.SaveSystemSchema(valueKinds);

        #endregion
        
        #endregion

        #region Auto resolve
        
        // Scan and register system schemas
        foreach (Assembly assembly in assemblies)
        {
            string defaultNs = assembly.GetMetaProperty<SchemaType>()?.Value 
                               ?? assembly.GetName().Name?.ToLowerInvariant() 
                                ?? throw new Exception($"Failed to get default namespace for assembly '{assembly.FullName}'");
            
            // scalar type first because the schema type is not the type declare it
            foreach (Type type in assembly.GetTypes().Where(t => t.IsSubclassOfGenericType(typeof(IScalarType<>))))
            {
                ResolveScalarSchema(type, defaultNs);
            }

            // other types
            foreach (Type type in assembly.GetTypes().Where(t => !t.IsSubclassOfGenericType(typeof(IScalarType<>))))
            {
                if (type.GetMetaProperty<SchemaType>() is not { HasValue: true }) continue;
                string _ = ResolveOtherSchema(type, defaultNs) ?? throw  new Exception($"Failed to resolve schema for type '{type}'");
            }
        }
        
        #endregion
        
        #endregion
        
        #region Utility

        string ResolveScalarSchema(Type type, string defaultNs)
        {
            string? name = runtime.GetTypeSchema(type);
            if  (!string.IsNullOrWhiteSpace(name)) return name;

            SchemaType? schemaType = type.GetMetaProperty<SchemaType>();
            name = schemaType?.Value ?? $"{defaultNs}.{type.Name}".ToLowerInvariant();

            // node schema
            NodeSchema nodeSchema = new ()
            {
                Name = name.GetSchemaName(),
                Namespace = name.GetNamespace(),
                Kind = nameof(ScalarSchema).GetSchemaKind()
            };
            nodeSchema.SetProperty<Display, LocaleString>(name);
            
            // scalar schema
            ScalarSchema scalarSchema = new ScalarSchema();
            
            // gets the type & equivalents
            nodeSchema.Type = type.GetGenericBaseType(typeof(IScalarType<>))?.GetGenericArguments()
                .ElementAtOrDefault(0);
            nodeSchema.Equivalents = type.GetMetaProperties<ClrEquivalent>().Where(p => p.HasValue)
                .Select(p => p.Value!).ToArray();
            
            // inherit the base type
            if (type.BaseType is { } superType)
                scalarSchema.Base = ResolveScalarSchema(superType, defaultNs);
            else if(nodeSchema.Type == null)
                throw new Exception($"Failed to get generic arguments for type '{type}'");
            
            // register scalar schema
            nodeSchema.SetProperty<ScalarProperty, ScalarSchema>(scalarSchema);
            runtime.SaveSystemSchema(ExtendSchema(nodeSchema, type));
            return name;
        }
        
        string? ResolveOtherSchema(Type type, string defaultNs)
        {
            string? name = runtime.GetTypeSchema(type);
            if  (!string.IsNullOrWhiteSpace(name)) return name;

            foreach (INodeSchemaGenerator generator in schemaGenerators)
            {
                NodeSchema[]? schemas = generator.GenerateSchema(type, defaultNs, ResolveOtherSchema);
                if (schemas == null || schemas.Length == 0) continue;
                foreach (NodeSchema schema in schemas)
                    runtime.SaveSystemSchema(schema.Type == type ? ExtendSchema(schema, type) : schema);
                return schemas.FirstOrDefault(s => s.Type == type)?.FullName;
            }
            return null;
        }

        // Save properties to the schema
        NodeSchema ExtendSchema(NodeSchema nodeSchema, Type type)
        {
            (string kind, Type nodeSchemaType, Type? propertyType)? info = nodeSchemaTypes.FirstOrDefault(t => nodeSchema.Kind.Equals(t.kind, StringComparison.OrdinalIgnoreCase));
            if (info?.propertyType == null) return nodeSchema;
            
            // get the property
            IProperty? property = nodeSchema.GetProperty(info.Value.propertyType);
            ExtensibleSchema? schema = property?.GetValue<ExtensibleSchema>();
            if (schema == null) return nodeSchema;
            foreach (IProperty prop in type.GetMetaProperties<IProperty>().Where(p =>
                 {
                     var metaProperty = p.GetType().GetMetaProperty<ForSchema>();
                     return metaProperty?.Value != null && metaProperty.Value.Contains(nodeSchema.Kind, StringComparer.OrdinalIgnoreCase);
                 }))
            {
                schema.SetProperty(prop);
            }
            
            // save back
            nodeSchema.SetProperty(property!);
            return nodeSchema;
        }
        
        #endregion
    }
}