using System.Reflection;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Constraint;
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

namespace SchemaNode.Generator;

public class WorkflowGenerator : INodeSchemaGenerator
{
    private static readonly NullabilityInfoContext _nullabilityContext = new();

    public IEnumerable<NodeSchema> GenerateSchema(SchemaRuntime runtime, Type type, string @namespace, string name, Func<Type, string, Type[]?, string?>? typeResolver = null)
    {
        if (!type.IsAssignableTo(typeof(BaseWorkflow))) yield break;
        
        NodeSchema schema = NodeSchema.Create(SCHEMA_KIND_WORKFLOW, @namespace, name, type);
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

        if (type.GetGenericArguments().Length > 0)
            throw new Exception($"Workflow type {type.FullName} can't be generic");

        // Settings
        Type? settingsType = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowSettings<>))?.GetGenericArguments()[0];
        workflowSchema.Settings = settingsType != null ? typeResolver(settingsType, @namespace, null) : null;
        
        // Session
        Type? sessionType = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowSession<>))?.GetGenericArguments()[0];
        workflowSchema.Session = sessionType != null ? typeResolver(sessionType, @namespace, null) : null;
        
        // Payload
        Type? payloadType = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowPayload<>))?.GetGenericArguments()[0];
        workflowSchema.Payload = payloadType != null ? typeResolver(payloadType, @namespace, null)  : null;
        
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
                arg.SetProperty<Display, LocaleString>(processMethod.GetSummaryFromXmlDoc(p) ??  $"{schema.FullName}.{arg.Name}");
                    
                // Default
                if (defaultProp?.Value != null)
                    arg.SetProperty<Default, object>(defaultProp.Value);
                    
                // Extension Properties
                foreach (IProperty property in p.GetMetaPropertiesForSchema<IProperty>(SCHEMA_KIND_FUNC_ARG))
                    arg.SetProperty(property);

                // Params
                bool isVariadic = p.IsDefined(typeof(ParamArrayAttribute), false);
                if (isVariadic)
                    arg.SetProperty<Variadic, bool>(true);

                // Check dynamic type
                arg.Type =  p.GetMetaProperty<SchemaType>()?.GetValue<string>() 
                            ?? typeResolver(isVariadic ? pt.CoreType : pt.Type, @namespace, null)
                            ?? throw new Exception($"Can't resolve parameter type for method {processMethod.Name} in {@type.FullName}");

                workflowSchema.Args[i] = arg;
            }
        }

        schema.SetProperty<WorkflowProperty, WorkflowSchema>(workflowSchema);
        yield return schema;
    }
}