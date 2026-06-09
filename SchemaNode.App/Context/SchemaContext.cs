using SchemaNode.Schema;
using SchemaNode.Enum;
using SchemaNode.Service;
using SchemaNode.Utility;
using AppType = SchemaNode.Runtime.AppType;

namespace SchemaNode.Context;

public static class AppSchemaContextExtension
{
    extension(SchemaContext context)
    {
        /// <summary>
        /// Gets the application node
        /// </summary>
        public async Task<AppType?> GetAppTypeAsync(string fullName, bool reload = false)
        {
            AppSchemaRuntime runtime = context.Runtime as AppSchemaRuntime ?? throw new InvalidOperationException();
            SpanReader spans = fullName;
            AppType? app = await LoadAppTypeAsync(runtime.RootAppType, spans);
            while (app != null && spans.NextPath())
                app = await LoadAppTypeAsync(app, spans);
            return app;

            async Task<AppType?> LoadAppTypeAsync(AppType root, SpanReader span)
            {
                ReadOnlySpan<char> next = span.Current;
                AppType? result = root;
                if (!next.IsEmpty)
                    result = root.GetAppType(next);
                
                // loading
                if (result is not { Loaded: true } || reload && span.IsEnd)
                {
                    string nextVal = next.IsEmpty ? "" : next.ToString();
                    AppSchema? schema = await LoadAppSchemaAsync(root != result ? root : null, nextVal);
                    if (schema == null) return null;

                    result ??= new AppType();
                
                    // cache by segment name (next), because result.Name is empty until LoadTypeAsync sets Schema
                    if (root != result)
                        root.SaveAppType(nextVal, result);

                    // Load the schema
                    context.LogDebug("[Runtime]App Type {schemaName} loading", schema.FullName);

                    await result.LoadAsync(context, schema);
                
                    // Namespace
                    if (schema.Apps is { Length: > 0 })
                        foreach (AppSchema s in schema.Apps)
                            result.SaveAppSchema(s);
                
                    context.LogDebug("[Runtime]App Type {schemaName} working", schema.FullName);
                }
                
                return result;
            }

            async Task<AppSchema?> LoadAppSchemaAsync(AppType? root, string name)
            {
                AppSchema? schema = root?.GetAppSchema(name);
                if (schema != null) return schema;

                string schemaName = $"{root?.Name}.{name}".Trim('.');
                schema = SetSchemaState(runtime.GetSystemAppSchema(schemaName), SchemaLoadState.System);
                if (context.SystemMode) return schema;

                foreach (var provider in context.GetServices<IAppSchemaProvider>())
                {
                    try
                    {
                        AppSchema? loadAppSchema = await provider.LoadAppSchemaAsync(schemaName);
                        if (loadAppSchema == null) continue;
                        AppSchema loadSchema = SetSchemaState(loadAppSchema, SchemaLoadState.Service, provider.GetType())!;

                        // check && combine
                        if (schema == null)
                        {
                            schema = loadSchema;
                            continue;
                        }

                        // Combine
                        schema.CombineExtensions(loadSchema, runtime);

                        if (schema.Apps == null || schema.Apps.Length == 0)
                        {
                            schema.Apps = loadSchema.Apps;
                            continue;
                        }
                        
                        if (loadSchema.Apps == null || schema.Apps.Length == 0) continue;

                        // combine
                        List<AppSchema>? otherSchemas = null;
                        foreach (var otherSchema in loadSchema.Apps)
                        {
                            int index = Array.FindIndex(schema.Apps,
                                s => s.Name.Equals(otherSchema.Name, StringComparison.OrdinalIgnoreCase));
                            if (index >= 0)
                            {
                                schema.Apps[index].CombineExtensions(otherSchema, runtime);
                            }
                            else
                            {
                                otherSchemas ??= [];
                                otherSchemas.Add(otherSchema);
                            }
                        }

                        if (otherSchemas != null)
                            schema.Apps = schema.Apps.Concat(otherSchemas).ToArray();
                    }
                    catch (Exception e)
                    {
                        context.LogError(e, $"Failed to load schema '{schemaName}' from schema provider '{provider.GetType().FullName}'.");
                    }
                }

                if (schema != null) root?.SaveAppSchema(schema);
                return schema;
            }
            
            AppSchema? SetSchemaState(AppSchema? schema, SchemaLoadState loadState, Type? provider = null)
            {
                schema?.Provider = provider;
                schema?.LoadState = loadState;
                if (schema?.Apps == null) return schema;
                foreach (var s in schema.Apps)
                    SetSchemaState(s, loadState, provider);
                return schema;
            }
        }
    }
}