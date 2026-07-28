using SchemaNode.Schema;
using SchemaNode.Enum;
using SchemaNode.Property.App;
using SchemaNode.Runtime;
using SchemaNode.Schema.Provider;
using SchemaNode.Utility;
using RuntimeAppType = SchemaNode.Runtime.AppType;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using AppType = SchemaNode.Schema.AppType;
using NamespaceType = SchemaNode.Runtime.NamespaceType;

namespace SchemaNode.Context;

public static class AppSchemaContextExtension
{
    extension(SchemaContext context)
    {
        /// <summary>
        /// Gets the application node
        /// </summary>
        public async Task<RuntimeAppType?> GetAppTypeAsync(string fullName, bool reload = false)
        {
            AppSchemaRuntime runtime = context.Runtime as AppSchemaRuntime ??
                                       throw new Exception("The schema run time is not an AppSchemaRuntime.");
            SpanReader spans = fullName;
            RuntimeAppType? app = await LoadAppTypeAsync(runtime.RootAppType, spans);
            while (app != null && spans.NextPath())
                app = await LoadAppTypeAsync(app, spans);
            return app;

            async Task<RuntimeAppType?> LoadAppTypeAsync(RuntimeAppType root, SpanReader span)
            {
                ReadOnlySpan<char> next = span.Current;
                RuntimeAppType? result = root;
                if (!next.IsEmpty)
                    result = root.GetAppType(next);
                if (result == null && reload || result?.Loaded == true && !(span.IsEnd && reload))
                    return result;

                // loading
                string nextVal = next.IsEmpty ? "" : next.ToString();
                AppSchema? schema = await LoadAppSchemaAsync(root != result ? root : null, nextVal);
                if (schema == null) return null;

                result ??= new RuntimeAppType { Container = root };

                // cache by segment name (next), because result.Name is empty until LoadTypeAsync sets Schema
                AppSchema[]? apps = schema.Apps;
                schema.Apps = null;
                if (root != result)
                {
                    root.SaveAppSchema(schema);
                    root.SaveAppType(nextVal, result);
                }

                // Load the schema
                context.LogDebug("[Runtime]App Type {schemaName} loading", schema.FullName);
                result.Loaded = true;
                await result.LoadAsync(context, schema);

                // Namespace
                if (apps is { Length: > 0 })
                    foreach (AppSchema s in apps)
                        result.SaveAppSchema(s);

                context.LogDebug("[Runtime]App Type {schemaName} working", schema.FullName);

                return result;
            }

            async Task<AppSchema?> LoadAppSchemaAsync(RuntimeAppType? root, string name)
            {
                // get loaded app schema from app container if not in reload mode
                AppSchema? schema = reload ? null : root?.GetAppSchema(name);
                if (schema != null) return schema;

                string schemaName = $"{root?.Name}.{name}".Trim('.');
                schema = SetSchemaState(runtime.GetSystemAppSchema(schemaName), SchemaLoadState.System);
                if (context.SystemMode) return schema;

                // 3rd app schema provider
                foreach (var provider in context.GetServices<IAppEntryProvider>())
                {
                    try
                    {
                        AppSchema? loadAppSchema = await provider.GetAppSchemaAsync(schemaName);
                        if (loadAppSchema == null) continue;
                        AppSchema loadSchema =
                            SetSchemaState(loadAppSchema, SchemaLoadState.Service, provider.GetType())!;

                        // check && combine
                        if (schema == null)
                        {
                            schema = loadSchema;
                            continue;
                        }

                        schema.LoadState |= loadSchema.LoadState;
                        schema.Provider ??= loadSchema.Provider;

                        // CombineProperties
                        schema.CombineProperties(loadSchema, runtime, SCHEMA_KIND_APP);

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
                                schema.Apps[index].CombineProperties(otherSchema, runtime, SCHEMA_KIND_APP);
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
                        context.LogError(e,
                            $"Failed to load schema '{schemaName}' from schema provider '{provider.GetType().FullName}'.");
                    }
                }

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

        /// <summary>
        /// Gets the node schema with auth properties
        /// </summary>
        public async Task<NodeSchema?> GetNodeSchemaAsync(NodeType type)
        {
            NodeSchema schema = type.GetNodeSchema(context.Runtime)!;
            bool canRead = await context.AuthorizeAsync(type, PolicyScope.SchemaRead, true);
            if (!canRead) return null;
            schema.SetProperty<SchemaRead, bool>(canRead);
            schema.SetProperty<SchemaCreate, bool>(await context.AuthorizeAsync(type, PolicyScope.SchemaCreate, true));
            schema.SetProperty<SchemaUpdate, bool>(await context.AuthorizeAsync(type, PolicyScope.SchemaUpdate, true));
            schema.SetProperty<SchemaDelete, bool>(await context.AuthorizeAsync(type, PolicyScope.SchemaDelete, true));
            return schema;
        }

        /// <summary>
        /// Gets all node schemas related by the node schema
        /// </summary>
        public async Task<NodeSchema> GetNodeSchemasAsync(NodeType nodeType,
            NodeSchema? root = null,
            HashSet<string>? types = null,
            bool fullNs = false,
            bool includeUsedBy = false,
            CancellationToken? cancellationToken = null)
        {
            types ??= [];
            root ??= new NodeSchema
            {
                Name = "",
                Kind = SCHEMA_KIND_NAMESPACE,
                Schemas = []
            };
            if (!types.Add(nodeType.Name) || nodeType is GenericType) return root;

            NodeSchema? schema = await context.GetNodeSchemaAsync(nodeType);
            if (schema == null) return root;
            
            // install full namespace path
            NodeSchema parent = root;
            if (nodeType.Namespace != null)
            {
                Stack<NamespaceType> namespaces = [];
                NamespaceType? n = nodeType.Namespace;
                while (n != null)
                {
                    namespaces.Push(n);
                    n = n.Namespace;
                }

                while (namespaces.TryPop(out n))
                {
                    parent.Schemas ??= [];
                    NodeSchema? sub = parent.Schemas.FirstOrDefault(s => n.Name.Equals(s.FullName, StringComparison.OrdinalIgnoreCase));
                    if (sub == null)
                    {
                        cancellationToken?.ThrowIfCancellationRequested();
                        sub = await context.GetNodeSchemaAsync(n);
                        if (sub == null) return root; // no read permission
                        parent.Schemas = parent.Schemas == null ? [sub] : parent.Schemas.Append(sub).ToArray();
                    }

                    parent = sub;
                }
            }

            if (includeUsedBy)
                schema.UsedBy = nodeType.GetUsedBy().Select(p => p.Name).ToArray();

            if (parent.Schemas == null ||
                !parent.Schemas.Any(s => s.FullName.Equals(schema.FullName, StringComparison.OrdinalIgnoreCase)))
            {
                parent.Schemas ??= [];
                parent.Schemas = parent.Schemas.Append(schema).ToArray();
            }

            if (nodeType is NamespaceType ns && fullNs)
            {
                foreach (NodeSchema s in ns.GetNodeSchemas())
                {
                    cancellationToken?.ThrowIfCancellationRequested();
                    NodeType? sns = await context.GetNodeTypeAsync(s.Name);
                    if (sns != null)
                        await context.GetNodeSchemasAsync(sns, root, types, fullNs, includeUsedBy, cancellationToken);
                }
            }

            // add references
            foreach (NodeType n in nodeType.GetReferenceTypes())
            {
                cancellationToken?.ThrowIfCancellationRequested();
                await context.GetNodeSchemasAsync(n, root, types, fullNs, includeUsedBy, cancellationToken);
            }

            return root;
        }

        /// <summary>
        /// Gets all node schemas used by the application
        /// </summary>
        /// <returns></returns>
        public async Task<NodeSchema[]> GetNodeSchemasAsync(Runtime.AppType app, NodeSchema? root = null,
            HashSet<string>? types = null, bool includeUsedBy = false, CancellationToken? cancellationToken = null)
        {
            types ??= [];
            root ??= new NodeSchema
            {
                Name = "",
                Kind = SCHEMA_KIND_NAMESPACE,
                Schemas = []
            };

            foreach (NodeType t in app.GetReferenceTypes())
            {
                cancellationToken?.ThrowIfCancellationRequested();
                await context.GetNodeSchemasAsync(t, root, types, false, includeUsedBy, cancellationToken);
            }

            return root.Schemas!;
        }
    }
}