using System.Reflection;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Function;
using SchemaNode.Property.Record;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Service;
using SchemaNode.Struct;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using SchemaNode.Workflow;
using SchemaNode.Property.App;

namespace SchemaNode.Generator;

public class WorkflowGenerator : INodeSchemaGenerator
{
    private static readonly NullabilityInfoContext _nullabilityContext = new();

    public IEnumerable<NodeSchema> GenerateSchema(SchemaRuntime runtime, Type type, string @namespace, string name, Func<Type, string, Type[]?, string?>? typeResolver = null)
    {
        if (!type.IsAssignableTo(typeof(BaseWorkflow))) yield break;
        
        NodeSchema schema = NodeSchema.Create(runtime, NODE_KIND_WORKFLOW, @namespace, name, type);
        if (typeResolver == null)
        {
            yield return schema;
            yield break;
        }
        
        // Workflow schema
        WorkflowSchema workflowSchema = new WorkflowSchema();

        // kind
        Type? current = type;
        while (current != null)
        {
            if (current.GetMetaProperty<WorkflowKind>() is { HasValue: true } kind)
            {
                workflowSchema.Kind = kind.Value!;
                break;
            }
            current = current.BaseType;
        }
        if (string.IsNullOrWhiteSpace(workflowSchema.Kind))
            workflowSchema.Kind = WORKFLOW_KIND_WORKFLOW;

        // Generic info
        Type[] genericArgs = type.GetGenericArguments();
        TypeDetail[] genInfos = genericArgs.Select(g => g.GetTypeDetail()).ToArray();
        GenericParameter[]? genericDeclare = type.GetMetaProperty<Generics>()?.Value;

        // Generics
        if (genericArgs.Length > 0 && genericDeclare is null)
        {
            genericDeclare = genInfos.Select(g => new GenericParameter(
                    typeResolver(g.CoreType, @namespace, genericArgs)!,
                    g.Number ? (g.OnlyFloat ? [NS_SYSTEM_NUMBER] : [NS_SYSTEM_NUMBER, NS_SYSTEM_INT]) : null
                )
            ).ToArray();
            workflowSchema.SetProperty<Generics, GenericParameter[]>(genericDeclare);
        }

        // Settings
        Type? settingsType = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowSettings<>))?.GetGenericArguments()[0];
        workflowSchema.Settings = settingsType != null ? typeResolver(settingsType, @namespace, genericArgs) : null;
        
        // Session
        Type? sessionType = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowSession<>))?.GetGenericArguments()[0];
        workflowSchema.Session = sessionType != null ? typeResolver(sessionType, @namespace, genericArgs) : null;
        
        // Payload
        Type? payloadType = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowPayload<>))?.GetGenericArguments()[0];
        workflowSchema.Payload = type.GetMetaProperty<PayloadType>()?.Value ?? (payloadType != null ? typeResolver(payloadType, @namespace, genericArgs)  : null);
        
        // Process args
        MethodInfo processMethod = type.GetMethod(BaseWorkflow.WORKFLOW_PROCESS_METHOD, BindingFlags.Public | BindingFlags.Instance) 
                                   ?? throw new Exception($"Can't find method ProcessAsync in {type.Name}");
    
        // must be async method, the first parameter is WorkflowContext
        // the second parameter is session if any
        ParameterInfo[] parameters = processMethod.GetParameters();
        if (parameters.Length == 0 || !parameters[0].ParameterType.IsAssignableTo(typeof(WorkflowContext)))
            throw new Exception($"Invalid ProcessAsync method in workflow type {type.FullName}");

        // match session type
        if (sessionType != null)
        {
            if (parameters.Length < 2 || !parameters[1].ParameterType.IsAssignableTo(sessionType))
                throw new Exception($"Invalid ProcessAsync method in workflow type {type.FullName}, session parameter mismatch");
            
            // check return type
            if (!processMethod.ReturnType.IsGenericType || processMethod.ReturnType.GetGenericTypeDefinition() != typeof(Task<>) ||
                !processMethod.ReturnType.GetGenericArguments()[0].IsAssignableTo(sessionType))
                throw new Exception($"Invalid ProcessAsync method in workflow type {type.FullName}, return type mismatch");
        }
        
        // Gather other parameters
        parameters = parameters.Skip(sessionType != null ? 2 : 1).ToArray();
        if (parameters.Length > 0)
        {
            List<RelationSchema> relations = [];
            TypeDetail[] paramInfos = parameters.Select(p => p.ParameterType.GetTypeDetail()).ToArray();
            workflowSchema.Args = new FuncArg[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo p = parameters[i];
                TypeDetail pt = paramInfos[i];
                Default? defaultProp = p.GetMetaProperty<Default>();
            
                FuncArg arg = new ()
                {
                    Name = p.Name ?? $"arg{i}"
                };
                
                // Require
                if (!(pt.Nullable || p.HasDefaultValue || _nullabilityContext.Create(p).ReadState == NullabilityState.Nullable ||
                      p.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute") != null ||
                      defaultProp != null || p.IsDefined(typeof(ParamArrayAttribute), false)))
                    arg.SetProperty<Require, bool>(true);
                
                // Display
                arg.SetProperty<Display, LocaleString>(processMethod.GetSummaryFromXmlDoc(p) ??  arg.Name);
                    
                // Default
                if (defaultProp?.Value != null)
                    arg.SetProperty<Default, object>(defaultProp.Value);
                    
                // Extension Properties
                foreach (IProperty property in p.GetMetaPropertiesForSchema<IProperty>(runtime, SCHEMA_KIND_NODE_FUNC_ARG))
                    arg.SetProperty(property);

                // Params
                bool isVariadic = p.IsDefined(typeof(ParamArrayAttribute), false);
                if (isVariadic)
                    arg.SetProperty<Variadic, bool>(true);

                // Check dynamic type
                arg.Type =  p.GetMetaProperty<SchemaType>()?.GetValue<string>() 
                            ?? typeResolver(isVariadic ? pt.CoreType : pt.Type, @namespace, genericArgs)
                            ?? throw new Exception($"Can't resolve parameter type for method {processMethod.Name} in {@type.FullName}");

                if (!(genericDeclare is { Length: > 0 } &&
                       genericDeclare.Any(g => g.Name.Equals(arg.Type, StringComparison.OrdinalIgnoreCase))))
                {
                    NodeSchema? fieldTypeSchema =
                        !string.IsNullOrWhiteSpace(arg.Type) ? runtime.GetSystemSchema(arg.Type) : null;
                    if (fieldTypeSchema == null)
                        throw new Exception($"Failed to resolve type for argument {arg.Name} of workflow {schema.FullName}");

                    if (pt.AnyArray && !fieldTypeSchema.Kind.Equals(NODE_KIND_ARRAY))
                        arg.Type = runtime.GetSystemArraySchema(arg.Type) ??
                                     throw new Exception($"Failed to resolve array schema for argument {arg.Name} of workflow {schema.FullName}");

                    // Extension Properties
                    foreach (IProperty property in p.GetMetaPropertiesForSchema<IProperty>(runtime, runtime.GetSchemaKindByNodeKind(fieldTypeSchema.Kind)))
                        arg.SetProperty(property);

                    if (fieldTypeSchema.Kind.Equals(NODE_KIND_ARRAY))
                    {
                        ArraySchema arraySchema = fieldTypeSchema.GetProperty<ArrayProperty>()?.Value
                                                  ?? throw new Exception($"Failed to get array schema for argument {arg.Name} of workflow {schema.FullName}");

                        string eleName = arraySchema.Element;
                        if (arraySchema.GetProperty<Generics>()?.Value is { } generics &&
                            generics.Any(g => g.Name.Equals(eleName, StringComparison.OrdinalIgnoreCase)))
                            eleName = runtime.GetSystemSchemaGenericArguments(fieldTypeSchema.FullName).FirstOrDefault() ??
                                      eleName;

                        NodeSchema? element = runtime.GetSystemSchema(eleName);
                        if (element != null)
                            foreach (IProperty property in p.GetMetaPropertiesForSchema<IProperty>(runtime, runtime.GetSchemaKindByNodeKind(element.Kind)))
                                arg.SetProperty(property);
                    }
                }


                // Argument Level Relations
                foreach (IRelationAttribute relation in p.GetCustomAttributes(inherit: false).OfType<IRelationAttribute>())
                    relations.Add(relation.GetRelationSchema(arg.Name));

                workflowSchema.Args[i] = arg;
            }
            // Function level relations
            foreach (var relation in processMethod.GetCustomAttributes(inherit: false).OfType<IRelationAttribute>())
                relations.Add((relation.GetRelationSchema(NODE_SELF)));

            foreach (var relation in type.GetCustomAttributes(inherit: false).OfType<IRelationAttribute>())
                relations.Add((relation.GetRelationSchema(NODE_SELF)));

            // Set relations
            if (relations.Count > 0)
                workflowSchema.SetProperty<Relations, RelationSchema[]>(relations.ToArray());
        }

        schema.SetProperty<WorkflowProperty, WorkflowSchema>(workflowSchema);
        yield return schema;
    }
}