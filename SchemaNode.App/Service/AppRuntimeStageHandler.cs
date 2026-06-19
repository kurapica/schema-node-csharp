using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartz;
using SchemaNode.Attribute;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Event;
using SchemaNode.Http;
using SchemaNode.Http.JsonRpc;
using SchemaNode.Property;
using SchemaNode.Property.App;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using SchemaNode.Workflow;
using Swashbuckle.AspNetCore.SwaggerGen;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Service;

/// <summary>
/// The stage handler to load app schemas into runtime
/// </summary>
public class AppRuntimeStageHandler: IRuntimeStageHandler
{
    public void OnServiceInitialization(IServiceProvider provider, IServiceCollection services, IEnumerable<Assembly> assemblies)
    {
        #region Config
        
        var options = provider.GetService<SchemaNodeConfig>();
        if (options is null)
        {
            options = new SchemaNodeConfig();
            services.AddSingleton(options);
        }

        #endregion
        
        #region critical region

        services.TryAddSingleton<ICriticalRegionProvider, LocalCriticalRegionProvider>();

        #endregion
        
        #region Quartz scheduler
        
        services.AddQuartz(q =>
        {
            q.UseInMemoryStore();
             
            q.UseDefaultThreadPool(tp =>
            {
                tp.MaxConcurrency = options.MaxQuartzConcurrentThreads;
            });
        });

        services.AddQuartzHostedService(opt =>
        {
            opt.WaitForJobsToComplete = true;
        });

        #endregion

        #region Context

        services.AddTransient<WorkflowContext>();

        #endregion
        
        #region Expression

        services.AddSingleton<IExpVisitor, DataSourceExpVisitor>();

        #endregion
        
        #region Api Protocol
        
        services.PostConfigure<SwaggerGenOptions>(c => c.DocumentFilter<SchemaApiDocumentFilter>());
        services.TryAddTransient<ISchemaApiProtocol, DefaultSchemaApiProtocol>();
        
        #endregion
        
        #region Api Types
        
        // schema api
        foreach (Type type in assemblies.SelectMany(a => a.GetTypes())
                     .Where(t => t.IsSubclassOfGenericType(typeof(SchemaApi<,>)) && !t.IsAbstract))
        {
            // Schema api
            Type apiBaseType = type.GetGenericBaseType(typeof(SchemaApi<,>))!;
            Type requestType = apiBaseType.GetGenericArguments()[0];
            Type responseType = apiBaseType.GetGenericArguments()[1];

            ApiTypes.Add(new SchemaApiType(type, requestType, responseType,
                type.GetCustomAttribute<NoProtocolAttribute>() != null));
            services.AddTransient(type);
        }
        
        #endregion
        
        #region Workflow
        
        services.TryAddSingleton<IEventDispatcher<BaseEvent>, DefaultEventDispatcher>();
        services.TryAddSingleton<IWorkflowScheduler, DefaultWorkflowScheduler>();
        services.TryAddScoped<IWorkflowContextPersistence, DynamicWorkflowContextPersistence>();
        
        #endregion
        
        #region Context Items

        services.AddScoped<Access>();
        services.AddScoped<AccessContextItemProvider>();

        #endregion
    }

    /// <summary>
    /// Generate system app with fields
    /// </summary>
    public async Task OnSystemSchemaLoaded(ISchemaContext context, IEnumerable<Assembly> assemblies)
    {
        if (context is not SchemaContext || context.Runtime is not AppSchemaRuntime runtime) return;
        
        // auto scan
        foreach (Assembly assembly in assemblies)
        {
            foreach (Type type in assembly.GetTypes())
            {
                if (runtime.GetTypeSchema(type) is { } schemaType &&
                    type.GetMetaProperty<App>() is { HasValue: true } app)
                {
                    string appName = app.Value!.ToLowerInvariant();

                    // Check application properties
                    var appProperties = type.GetMetaPropertiesForSchema<IProperty>(SCHEMA_KIND_APP).ToArray();
                    if (appProperties.Length > 0)
                    {
                        AppSchema appSchema = new AppSchema
                        {
                            Name = appName.GetSchemaName(), // for simple
                            Container = appName.GetNamespace(), // for 
                        };
                        foreach (var prop in appProperties)
                            appSchema.SetProperty(prop);
                        runtime.SaveSystemAppSchema(appSchema);
                    }

                    // Try using array type if primary index specified
                    schemaType = runtime.GetSystemArraySchema(schemaType, true) ?? schemaType;
                    NodeSchema? typeSchema = runtime.GetSystemSchema(schemaType);
                    if (typeSchema == null)
                        throw new Exception($"The schema type for {type.FullName} is not registered in runtime");
                    
                    AppFieldSchema field = new AppFieldSchema
                    {
                        App = appName,
                        Name = type.Name.ToLowerInvariant(),
                        Type = schemaType,
                    };
                    
                    // app field property
                    foreach (IProperty property in type.GetMetaPropertiesForSchema<IProperty>(SCHEMA_KIND_APP_FIELD))
                        field.SetProperty(property);
                    
                    // schema type property
                    foreach (IProperty property in type.GetMetaPropertiesForSchema<IProperty>(typeSchema.Kind))
                        field.SetProperty(property);
                    
                    runtime.SaveSystemAppFieldSchema(field, type);
                }
                
                // schema format provider
                else if (type.IsAssignableTo(typeof(ISchemaFormatProvider)))
                {
                    ISchemaFormatProvider.AddSchemaFormatProvider(type);
                }
            }
        }
    }
    
    static readonly List<SchemaApiType> ApiTypes = new();
    record SchemaApiType(Type Api, Type Request, Type Response, bool UseDefaultProtocol);
}