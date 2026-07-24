using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartz;
using SchemaNode.Attribute;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Event;
using SchemaNode.Http;
using SchemaNode.Property;
using SchemaNode.Property.App;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using SchemaNode.Workflow;
using Swashbuckle.AspNetCore.SwaggerGen;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using DataCombine = SchemaNode.Schema.DataCombine;

namespace SchemaNode.Service;

/// <summary>
/// The stage handler to load app schemas into runtime
/// </summary>
public class AppRuntimeStageHandler : IRuntimeStageHandler
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
            Http.Service.AddApiType(type);
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
        if (context is not SchemaContext schemaContext || context.Runtime is not AppSchemaRuntime runtime) return;

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
                    NodeSchema? typeSchema = runtime.GetSystemSchema(schemaType);
                    if (typeSchema == null)
                        throw new Exception($"The schema type for {type.FullName} is not registered in runtime");

                    AppFieldSchema field = new AppFieldSchema
                    {
                        App = appName,
                        Name = type.Name.ToLowerInvariant(),
                        Type = runtime.GetSystemArraySchema(schemaType, true) ?? schemaType,
                    };

                    // app field property
                    foreach (IProperty property in type.GetMetaPropertiesForSchema<IProperty>(SCHEMA_KIND_APP_FIELD))
                        field.SetProperty(property);

                    // schema type property
                    foreach (IProperty property in type.GetMetaPropertiesForSchema<IProperty>(typeSchema.Kind))
                        field.SetProperty(property);

                    // Data combine rules
                    if (typeSchema.Kind == SCHEMA_KIND_STRUCT)
                    {
                        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                        var structSchema = typeSchema.GetProperty<StructProperty>()?.Value;
                        if (structSchema != null)
                        {
                            List<DataCombine> combineRules = [];
                            foreach (StructFieldSchema f in structSchema.Fields)
                            {
                                if (properties.FirstOrDefault(x => x.Name.Equals(f.Name, StringComparison.OrdinalIgnoreCase)) is {} p)
                                {
                                    var combine = p.GetMetaProperty<SchemaNode.Property.App.DataCombine>();
                                    if (combine is { HasValue: true })
                                        combineRules.Add(new DataCombine(f.Name, combine.Value));
                                }
                            }
                            if (combineRules.Count > 0)
                                field.Combines = combineRules.ToArray();
                        }
                    }
                    
                    // push
                    if (type.GetMetaProperty<Push>() is { HasValue: true } push)
                    {
                        field.Push = push.Value!.Push;
                        field.Source = push.Value!.Source;
                    }
                    
                    runtime.SaveSystemAppFieldSchema(field, type);
                }

                // schema format provider
                else if (type.IsAssignableTo(typeof(ISchemaFormatProvider)))
                {
                    ISchemaFormatProvider.AddSchemaFormatProvider(type);
                }
            }
        }
        
        // loading system apps
        await LoadAllAppTypes("");
        
        async Task LoadAllAppTypes(string fullName)
        {
           var appType = await schemaContext.GetAppTypeAsync(fullName);
           if (appType == null) return;
           runtime.SaveSystemApp(appType);
            
            foreach (var schema in appType.GetSubAppSchemas())
                await LoadAllAppTypes(schema.FullName);
        }
    }

    /// <summary>
    /// Active the workflows
    /// </summary>
    public async Task OnActivatingAsync(ISchemaContext context)
    {
        if (context is not SchemaContext schemaContext || context.Runtime is not AppSchemaRuntime runtime) return;
        AppWorkflowQueue? workflowQueue = runtime.GetRuntimeItem<AppWorkflowQueue>();
        if (workflowQueue == null) return;
        while (workflowQueue.TryDequeue(out AppWorkflowType? workflowType))
            await workflowType.LoadAsync(schemaContext);
    }
}