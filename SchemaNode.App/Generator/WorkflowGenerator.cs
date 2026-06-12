using System.Reflection;
using SchemaNode.Attribute;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Property;
using SchemaNode.Property.Core;
using SchemaNode.Property.Record;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Service;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Generator;

public class WorkflowGenerator : INodeSchemaGenerator
{
    public IEnumerable<NodeSchema> GenerateSchema(SchemaRuntime runtime, Type type, string @namespace, string name, Func<Type, string, Type[]?, string?> typeResolver)
    {
        if (!type.IsAssignableTo(typeof(Workflow))) yield break;
        
        NodeSchema schema = NodeSchema.Create(SCHEMA_KIND_WORKFLOW, @namespace, name, type);
        WorkflowSchema workflowSchema = new WorkflowSchema();
        
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
        
        // no kind no workflow
        if (string.IsNullOrWhiteSpace(workflowSchema.Kind))
            throw new Exception($"Invalid workflow type {type.FullName} without workflow kind");

        // State
        Type? stateType = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowState<>))?.GetGenericArguments()[0];
        workflowSchema.State = stateType != null ? typeResolver(stateType, @namespace, null) : null;
        
        // Session
        Type? sessionType = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowSession<>))?.GetGenericArguments()[0];
        workflowSchema.Session = sessionType != null ? typeResolver(sessionType, @namespace, null) : null;
        
        // Payload
        Type? payloadType = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowPayload<>))?.GetGenericArguments()[0];
        if (payloadType != null)
            workflowSchema.Payload = typeResolver(payloadType, @namespace, null);
        else if (type.GetInterfaces().Any(i => i == typeof(IWorkflowPayload)))
        {
            workflowSchema.SetProperty<Generics, GenericParameter[]>([ new GenericParameter(NS_GENERIC_TYPE)]);
            workflowSchema.Payload = NS_GENERIC_TYPE;
        }
        
        // Process args
        MethodInfo processMethod = type.GetMethod(Workflow.WORKFLOW_PROCESS_METHOD, BindingFlags.Public | BindingFlags.Instance)
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
            {
                throw new Exception($"Invalid ProcessAsync method in workflow type {type.FullName}, return type mismatch");
            }
        }
        
        // Gather other parameters
        parameters = parameters.Skip(sessionType != null ? 2 : 1).ToArray();
        if (parameters.Length > 0)
        {
            workflowSchema.Args = new FuncArg[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo param = parameters[i];
                
                Utility.Schema.SchemaParamTypeInfo? info = param.ParameterType.GetSchemaTypeInfo(true, defaultNs: ns);
                if (info == null)
                    throw new Exception($"Unsupported parameter type {param.ParameterType.FullName} in ProcessAsync method of workflow type {type.FullName}");

                SchemaAttribute? attr = param.GetCustomAttribute<SchemaAttribute>();
                bool isParams = param.IsDefined(typeof(ParamArrayAttribute), false);

                workflowSchema.Args[i] = new FuncArg
                {
                    Name = param.Name ?? $"arg{i}",
                    Type = attr?.Name 
                        ?? (isParams && info.SchemaType != null && info.SchemaType.EndsWith("s") && Utility.Schema.GetSystemNodeSchema(info.SchemaType)?.Type == SchemaType.Array ? info.SchemaType[..^1] : info.SchemaType)
                        ?? throw new Exception($"Unsupported parameter type {param.ParameterType.FullName} in ProcessAsync method of workflow type {type.FullName}"),
                    Nullable = info.Nullable || param is { HasDefaultValue: true, DefaultValue: null },
                    Params = isParams ? true : null,
                };
            }
        }

        schema.SetProperty<WorkflowProperty, WorkflowSchema>(workflowSchema);
        yield return schema;
    }
}