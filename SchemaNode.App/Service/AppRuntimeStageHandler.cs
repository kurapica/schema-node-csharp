using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartz;
using SchemaNode.Attribute;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Property;
using SchemaNode.Property.App;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Service;

/// <summary>
/// The stage handler to load app schemas into runtime
/// </summary>
public class AppRuntimeStageHandler: IRuntimeStageHandler
{
    public void OnServiceInitialization(IServiceProvider provider, IServiceCollection services)
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
            }
        }
    }
}