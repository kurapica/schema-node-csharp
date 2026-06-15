using System.Reflection;
using SchemaNode.Attribute;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Record;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Service;
using SchemaNode.Utility;
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
        
        // generic types
        Type[] generics = type.GetGenericArguments();
        if (generics.Length > 0)
            workflowSchema.SetProperty<Generics, GenericParameter[]>(
                generics.Select(g => g.GetTypeDetail()).Select(g => 
                    new GenericParameter (
                        typeResolver(g.CoreType, @namespace, generics)!,
                        g.Number ? [g.OnlyFloat ? NS_SYSTEM_DOUBLE : NS_SYSTEM_NUMBER] : null
                    )
                ).ToArray());

        // State
        Type? stateType = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowState<>))?.GetGenericArguments()[0];
        workflowSchema.State = stateType != null ? typeResolver(stateType, @namespace, generics) : null;
        
        // Session
        Type? sessionType = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowSession<>))?.GetGenericArguments()[0];
        workflowSchema.Session = sessionType != null ? typeResolver(sessionType, @namespace, null) : null;
        
        // Payload
        Type? payloadType = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowPayload<>))?.GetGenericArguments()[0];
        workflowSchema.Payload = payloadType != null ? typeResolver(payloadType, @namespace, null)  : null;
        
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
                    Name = p.Name ?? $"arg{i}",
                    Nullable = pt.Nullable || p.HasDefaultValue || new NullabilityInfoContext().Create(p).ReadState == NullabilityState.Nullable ||
                               p.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute") != null ||
                               defaultProp != null,
                    Display = processMethod.GetSummaryFromXmlDoc(p) ?? null,
                    Default = defaultProp?.Value, // not the default value of the parameter
                };

                // Params
                if (p.IsDefined(typeof(ParamArrayAttribute), false))
                {
                    arg.Params = true;
                    arg.Nullable = true;
                }

                // Check dynamic type
                arg.Type =  p.GetMetaProperty<SchemaType>()?.GetValue<string>() 
                            ?? typeResolver(arg.Params == true ? pt.CoreType : pt.Type, @namespace, generics)
                            ?? throw new Exception($"Can't resolve parameter type for method {processMethod.Name} in {@type.FullName}");

                workflowSchema.Args[i] = arg;
            }
        }

        schema.SetProperty<WorkflowProperty, WorkflowSchema>(workflowSchema);
        yield return schema;
    }
}