using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.AI;

/// <summary>
/// Extension methods on <see cref="SchemaContext"/> that build an <see cref="OntologyGraph"/>
/// from the App schema hierarchy and render it via <see cref="OntologyTextTemplates"/>.
/// </summary>
public static class SchemaContextOntologyExtension
{
    internal const string DefaultBaseUri = "https://schema.local/";

    /// <summary>
    /// Builds an <see cref="OntologyGraph"/> for <paramref name="appName"/>.
    /// All schema type references (<c>SchemaType</c>, <c>ElementSchemaType</c>, <c>SchemaType</c>)
    /// are already resolved when the App is loaded, so no extra async resolution is needed.
    /// </summary>
    /// <param name="context">Active schema context.</param>
    /// <param name="appName">Root application name.</param>
    /// <param name="baseUri">Base IRI namespace root. Defaults to <c>https://schema.local/</c>.</param>
    /// <param name="includeSubApps">Whether to recurse into sub-applications.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<OntologyGraph> BuildAppOntologyAsync(
        this SchemaContext context,
        string appName,
        string baseUri = DefaultBaseUri,
        bool includeSubApps = true,
        CancellationToken cancellationToken = default)
    {
        if (!baseUri.EndsWith('/')) baseUri += '/';

        var graph = new OntologyGraph { AppName = appName, BaseUri = baseUri };

        var visitedApps    = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visitedStructs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visitedEnums   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visitedCtx     = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // preload:true ensures the app and all sub-apps are fully loaded (fields + resolved types)
        AppType? root = await context.GetAppTypeAsync(appName, preload: true);
        if (root != null)
            await BuildAppClassAsync(context, graph, root, null, includeSubApps,
                visitedApps, visitedStructs, visitedEnums, visitedCtx, cancellationToken);

        ResolveFkRanges(graph);
        return graph;
    }

    /// <summary>
    /// Shorthand: build and immediately render the ontology for <paramref name="appName"/>.
    /// </summary>
    public static async Task<string> RenderAppOntologyAsync(
        this SchemaContext context,
        string appName,
        string format = OntologyTextTemplates.FormatTurtle,
        string baseUri = DefaultBaseUri,
        bool includeSubApps = true,
        CancellationToken cancellationToken = default)
    {
        OntologyGraph graph = await context.BuildAppOntologyAsync(
            appName, baseUri, includeSubApps, cancellationToken);
        return OntologyTextTemplates.Render(graph, format);
    }

    /// <summary>
    /// Builds an <see cref="OntologyGraph"/> from a schema type or namespace.
    /// <list type="bullet">
    ///   <item>If <paramref name="schemaName"/> resolves to a <see cref="TypeNamespace"/> all
    ///     concrete descendant types are included recursively.</item>
    ///   <item>If it resolves to a concrete type (<see cref="StructType"/>, <see cref="EnumType"/>,
    ///     <see cref="ScalarType"/>, <see cref="FunctionType"/>) only that type is included.</item>
    /// </list>
    /// </summary>
    public static async Task<OntologyGraph> BuildSchemaOntologyAsync(
        this SchemaContext context,
        string schemaName,
        string baseUri = DefaultBaseUri,
        CancellationToken cancellationToken = default)
    {
        if (!baseUri.EndsWith('/')) baseUri += '/';

        var graph = new OntologyGraph { AppName = schemaName, BaseUri = baseUri };

        var visitedStructs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visitedEnums   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visitedScalars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visitedFuncs   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AnySchemaType? type = await context.GetSchemaTypeAsync(schemaName, preload: true);
        if (type != null)
            await CollectSchemaTypesAsync(context, graph, type,
                visitedStructs, visitedEnums, visitedScalars, visitedFuncs, cancellationToken);

        ResolveFkRanges(graph);
        return graph;
    }

    /// <summary>
    /// Shorthand: build and immediately render the schema/namespace ontology.
    /// </summary>
    public static async Task<string> RenderSchemaOntologyAsync(
        this SchemaContext context,
        string schemaName,
        string format = OntologyTextTemplates.FormatTurtle,
        string baseUri = DefaultBaseUri,
        CancellationToken cancellationToken = default)
    {
        OntologyGraph graph = await context.BuildSchemaOntologyAsync(schemaName, baseUri, cancellationToken);
        return OntologyTextTemplates.Render(graph, format);
    }

    // -------------------------------------------------------------------------
    #region Schema type builder

    /// <summary>
    /// Dispatches <paramref name="type"/> to the appropriate builder.
    /// For <see cref="TypeNamespace"/> the method recurses into every direct child.
    /// </summary>
    private static async Task CollectSchemaTypesAsync(
        SchemaContext context,
        OntologyGraph graph,
        AnySchemaType type,
        HashSet<string> visitedStructs,
        HashSet<string> visitedEnums,
        HashSet<string> visitedScalars,
        HashSet<string> visitedFuncs,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (type)
        {
            case TypeNamespace ns:
                foreach (NodeSchema child in ns.Schemas)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AnySchemaType? childType = await context.GetSchemaTypeAsync(child.Name, preload: true);
                    if (childType == null) continue;
                    await CollectSchemaTypesAsync(context, graph, childType,
                        visitedStructs, visitedEnums, visitedScalars, visitedFuncs, cancellationToken);
                }
                break;

            case StructType st:
                BuildEntityClass(context, graph, st, visitedStructs, visitedEnums);
                break;

            case EnumType et:
                BuildEnumClass(context, graph, et, visitedEnums);
                break;

            case ScalarType sc:
                BuildScalarClass(graph, sc, visitedScalars);
                break;

            case FunctionType ft:
                BuildFunctionClass(graph, ft, visitedFuncs, visitedStructs, visitedEnums);
                break;
        }
    }

    private static void BuildScalarClass(
        OntologyGraph graph,
        ScalarType scalar,
        HashSet<string> visitedScalars)
    {
        if (!visitedScalars.Add(scalar.Name)) return;

        string seg = Seg(scalar.Name);
        graph.ScalarClasses.Add(new OntologyScalarClass
        {
            Name     = seg,
            Iri      = $"{graph.AppPrefix}{seg}",
            Labels   = ToLabels(scalar.Display),
            BaseType = ScalarToXsd(scalar.Name),
            LowLimit = scalar.GetLowlimit<decimal>(),
            UpLimit  = scalar.GetUplimit<decimal>(),
            Unit     = scalar.Unit?.Key,
            Pattern  = scalar.Pattern,
        });
    }

    private static void BuildFunctionClass(
        OntologyGraph graph,
        FunctionType func,
        HashSet<string> visitedFuncs,
        HashSet<string> visitedStructs,
        HashSet<string> visitedEnums)
    {
        if (!visitedFuncs.Add(func.Name)) return;

        string seg = Seg(func.Name);

        OntologyFunctionArg[] args = func.Args.Select(a => new OntologyFunctionArg
        {
            Name       = a.Name,
            TypeStr    = ResolveArgTypeStr(a, graph, visitedStructs, visitedEnums),
            Labels     = ToLabels(a.Display),
            IsNullable = a.Nullable == true,
            IsParams   = a.Params == true,
        }).ToArray();

        string returnTypeStr = func.ReturnNode != null
            ? ResolveSchemaTypeStr(func.ReturnNode)
            : !string.IsNullOrEmpty(func.Return) ? func.Return : "void";

        graph.FunctionClasses.Add(new OntologyFunctionClass
        {
            Name          = seg,
            Iri           = $"{graph.PropPrefix}{seg}",
            Labels        = ToLabels(func.Display),
            Args          = args,
            ReturnTypeStr = returnTypeStr,
            IsPure        = func.SideEffect != true,
            IsConverter   = func.Converter == true,
            IsWorkflowOnly = func.WorkflowOnly == true,
            HasSideEffect = func.SideEffect == true,
        });
    }

    private static string ResolveArgTypeStr(
        FunctionNodeArgument arg,
        OntologyGraph graph,
        HashSet<string> visitedStructs,
        HashSet<string> visitedEnums)
    {
        if (arg.SchemaType != null)
            return ResolveSchemaTypeStr(arg.SchemaType);
        return !string.IsNullOrWhiteSpace(arg.Type) ? arg.Type : "Any";
    }

    private static string ResolveSchemaTypeStr(AnySchemaType type) => type switch
    {
        StructType st  => $"app:{Seg(st.Name)}",
        EnumType et    => $"en:{Seg(et.Name)}",
        ScalarType sc  => ScalarToXsd(sc.Name),
        ArrayType ar   => ar.ElementSchemaType != null
                            ? $"[{ResolveSchemaTypeStr(ar.ElementSchemaType)}]"
                            : "Array",
        _              => type.Name,
    };

    #endregion

    // -------------------------------------------------------------------------
    #region App class builder

    private static async Task BuildAppClassAsync(
        SchemaContext context,
        OntologyGraph graph,
        AppType app,
        string? parentClassIri,
        bool includeSubApps,
        HashSet<string> visitedApps,
        HashSet<string> visitedStructs,
        HashSet<string> visitedEnums,
        HashSet<string> visitedCtx,
        CancellationToken cancellationToken)
    {
        if (!visitedApps.Add(app.Name)) return;
        cancellationToken.ThrowIfCancellationRequested();

        string className = Seg(app.Name);
        string classIri  = $"{graph.AppPrefix}{className}";

        var cls = new OntologyAppClass
        {
            Name      = className,
            Iri       = classIri,
            Labels    = ToLabels(app.Display),
            Comment   = app.Desc?.Key,
            ParentIri = parentClassIri,
        };

        //Fields: each field = one DB table
        if (app.Fields is { Count: > 0 })
        {
            foreach (AppFieldType field in app.Fields)
            {
                cancellationToken.ThrowIfCancellationRequested();

                AnySchemaType? schemaType = field.SchemaType;
                bool isMulti = false;

                // Unwrap array → get element type
                if (schemaType is ArrayType arr)
                {
                    isMulti    = true;
                    schemaType = arr.ElementSchemaType;
                }

                string           rangeIri;
                OntologyPropertyKind kind;

                switch (schemaType)
                {
                    case StructType st:
                        rangeIri = $"app:{Seg(st.Name)}";
                        kind     = OntologyPropertyKind.Object;
                        BuildEntityClass(context, graph, st, visitedStructs, visitedEnums);
                        break;

                    case EnumType et:
                        rangeIri = BuildEnumClass(context, graph, et, visitedEnums);
                        kind     = OntologyPropertyKind.Data;
                        break;

                    case ScalarType sc:
                        rangeIri = ScalarToXsd(sc.Name);
                        kind     = OntologyPropertyKind.Data;
                        break;

                    default:
                        rangeIri = "xsd:string";
                        kind     = OntologyPropertyKind.Data;
                        break;
                }

                cls.Tables.Add(new OntologyTableField
                {
                    Name         = field.Name,
                    Labels       = ToLabels(field.Display),
                    Comment      = field.Desc?.Key,
                    RangeIri     = rangeIri,
                    Kind         = kind,
                    IsMultiValued = isMulti,
                    IsComputed   = !string.IsNullOrEmpty(field.Func),
                });
            }
        }

        // Relation annotations
        if (app.Relations is { Count: > 0 })
        {
            foreach (AppRelationSchema rel in app.Relations)
            {
                string fieldRef = !string.IsNullOrEmpty(rel.DataField)
                    ? $"{rel.AppField}.{rel.DataField}"
                    : rel.AppField;

                cls.Relations.Add(new OntologyRelation
                {
                    Field        = fieldRef,
                    Property     = rel.Prop,
                    Function     = rel.Func,
                    Args         = rel.Args.Select(a =>
                        !string.IsNullOrEmpty(a.DataField)
                            ? $"{a.AppField}.{a.DataField}"
                            : !string.IsNullOrEmpty(a.AppField)
                                ? a.AppField
                                : a.Value?.ToString() ?? "").ToArray(),
                });
            }
        }

        graph.AppClasses.Add(cls);

        // Scope relations (ownership / context-scoping ObjectProperties)
        BuildScopeRelations(graph, app, cls, visitedCtx);

        // Sub-apps
        if (!includeSubApps) return;

        if (app.SubAppList is { Count: > 0 })
        {
            foreach (AppType sub in app.SubAppList.Values)
                await BuildAppClassAsync(context, graph, sub, classIri, includeSubApps,
                    visitedApps, visitedStructs, visitedEnums, visitedCtx, cancellationToken);
        }
        else if (app.Apps is { Length: > 0 })
        {
            // SubAppList not yet populated — load each sub-app individually
            foreach (AppSchema subSchema in app.Apps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AppType? sub = await context.GetAppTypeAsync(subSchema.Name, preload: true);
                if (sub != null)
                    await BuildAppClassAsync(context, graph, sub, classIri, includeSubApps,
                        visitedApps, visitedStructs, visitedEnums, visitedCtx, cancellationToken);
            }
        }
    }

    #endregion

    // -------------------------------------------------------------------------
    #region Scope relation builder

    /// <summary>
    /// Derives <see cref="OntologyScopeRelation"/> entries from <paramref name="app"/>'s
    /// <c>ScopePolicy</c> and appends them to <paramref name="cls"/>.
    /// <para>
    /// Mapping rules:
    /// <list type="bullet">
    ///   <item><b>SystemLevel</b> — no relation (data is global).</item>
    ///   <item><b>BusinessTarget</b> — Composition: one <c>owl:FunctionalProperty</c> pair
    ///     pointing to <c>app:TargetEntity</c>, <c>rdfs:subPropertyOf schema:isPartOf</c>.</item>
    ///   <item><b>IsolationContext (1 map)</b> — Aggregation: one ObjectProperty pair,
    ///     <c>rdfs:subPropertyOf schema:isPartOf</c>, <c>owl:minCardinality 1</c>.</item>
    ///   <item><b>IsolationContext (N maps)</b> — Association: one ObjectProperty pair per axis,
    ///     no <c>subPropertyOf</c>, <c>owl:minCardinality 1</c> each.</item>
    /// </list>
    /// </para>
    /// </summary>
    private static void BuildScopeRelations(
        OntologyGraph graph,
        AppType app,
        OntologyAppClass cls,
        HashSet<string> visitedCtx)
    {
        switch (app.ScopeType)
        {
            case AppScopeType.SystemLevel:
                return; // No ownership semantics for system-level apps

            case AppScopeType.BusinessTarget:
            {
                // Composition: exactly-one lifecycle-bound ownership by an abstract target entity
                const string entityName = "TargetEntity";
                EnsureContextEntity(graph, visitedCtx, entityName,
                    "Abstract business-target entity that owns App data (lifecycle-bound composition).");

                cls.ScopeRelations.Add(new OntologyScopeRelation
                {
                    ForwardProperty = $"prop:{cls.Name}.target",
                    InverseProperty = $"prop:{cls.Name}.targetOf",
                    DomainIri       = cls.Iri,
                    RangeIri        = $"app:{entityName}",
                    Kind            = OntologyScopeRelationKind.Composition,
                    SubPropertyOf   = "schema:isPartOf",
                    IsFunctional    = true,
                    ContextItem     = "Access.Target",
                });
                break;
            }

            case AppScopeType.IsolationContext
                when app.ScopePolicy?.ContextMaps is { Length: > 0 } maps:
            {
                bool isMulti = maps.Length > 1;
                OntologyScopeRelationKind kind = isMulti
                    ? OntologyScopeRelationKind.Association
                    : OntologyScopeRelationKind.Aggregation;

                foreach (AppScopeContextMap map in maps)
                {
                    // Derive entity concept name from the last path segment, stripping "Id" suffix
                    string[] parts   = map.ContextItem.Split('.', StringSplitOptions.RemoveEmptyEntries);
                    string   lastSeg = parts.Length > 0 ? parts[^1] : map.ContextItem;
                    string   baseName = lastSeg.EndsWith("Id", StringComparison.OrdinalIgnoreCase) && lastSeg.Length > 2
                        ? lastSeg[..^2]
                        : lastSeg;

                    string entityName = Seg(baseName);
                    EnsureContextEntity(graph, visitedCtx, entityName,
                        $"Context isolation entity derived from scope item '{map.ContextItem}'.");

                    // Prop local name: prefer MapKey (strip leading "_"), else camelCase of baseName
                    string propLocal = !string.IsNullOrWhiteSpace(map.MapKey)
                        ? map.MapKey.TrimStart('_')
                        : baseName.Length > 0
                            ? char.ToLowerInvariant(baseName[0]) + baseName[1..]
                            : lastSeg;

                    cls.ScopeRelations.Add(new OntologyScopeRelation
                    {
                        ForwardProperty = $"prop:{cls.Name}.{propLocal}",
                        InverseProperty = $"prop:{cls.Name}.{propLocal}Of",
                        DomainIri       = cls.Iri,
                        RangeIri        = $"app:{entityName}",
                        Kind            = kind,
                        SubPropertyOf   = isMulti ? null : "schema:isPartOf",
                        IsFunctional    = false,
                        ContextItem     = map.ContextItem,
                    });
                }
                break;
            }
        }
    }

    private static void EnsureContextEntity(
        OntologyGraph graph,
        HashSet<string> visited,
        string name,
        string comment)
    {
        if (!visited.Add(name)) return;
        graph.ContextEntityClasses.Add(new OntologyContextEntityClass
        {
            Name    = name,
            Iri     = $"{graph.AppPrefix}{name}",
            Comment = comment,
        });
    }

    #endregion

    // -------------------------------------------------------------------------
    #region Entity (struct) class builder

    /// <summary>
    /// Recursively builds an <see cref="OntologyEntityClass"/> for <paramref name="structType"/>
    /// and any nested struct / base struct it references.
    /// Uses <c>SchemaType</c> — already resolved during <c>StructType.LoadAsync</c>.
    /// </summary>
    private static void BuildEntityClass(
        SchemaContext context,
        OntologyGraph graph,
        StructType structType,
        HashSet<string> visitedStructs,
        HashSet<string> visitedEnums)
    {
        if (!visitedStructs.Add(structType.Name)) return;

        string className = Seg(structType.Name);
        string classIri  = $"{graph.AppPrefix}{className}";

        // Handle inheritance
        string? baseClassIri = null;
        /*if (structType.BaseNode != null)
        {
            baseClassIri = $"{graph.AppPrefix}{Seg(structType.BaseNode.Name)}";
            BuildEntityClass(context, graph, structType.BaseNode, visitedStructs, visitedEnums);
        }*/

        var entity = new OntologyEntityClass
        {
            Name         = className,
            Iri          = classIri,
            Labels       = ToLabels(structType.Display),
            BaseClassIri = baseClassIri,
        };

        foreach (StructFieldSchema field in structType.Fields)
        {
            AnySchemaType? fieldType = field.SchemaType;
            bool isMulti = false;

            // Unwrap array element
            if (fieldType is ArrayType arr)
            {
                isMulti   = true;
                fieldType = arr.ElementSchemaType;
            }

            string           rangeIri;
            OntologyPropertyKind kind;

            switch (fieldType)
            {
                case ScalarType sc:
                    rangeIri = ScalarToXsd(sc.Name);
                    kind     = OntologyPropertyKind.Data;
                    break;

                case EnumType et:
                    // SKOS Concepts are OWL individuals — owl:ObjectProperty
                    rangeIri = BuildEnumClass(context, graph, et, visitedEnums);
                    kind     = OntologyPropertyKind.Object;
                    break;

                case StructType nested:
                    rangeIri = $"app:{Seg(nested.Name)}";
                    kind     = OntologyPropertyKind.Object;
                    BuildEntityClass(context, graph, nested, visitedStructs, visitedEnums);
                    break;

                default:
                    rangeIri = "xsd:string";
                    kind     = OntologyPropertyKind.Data;
                    break;
            }

            // Detect foreign-key pattern: scalar xsd:string/integer field ending in "Id" (case-sensitive)
            bool isFk = kind == OntologyPropertyKind.Data
                && (rangeIri == "xsd:string" || rangeIri == "xsd:integer")
                && field.Name.EndsWith("Id", StringComparison.Ordinal)
                && field.Name.Length > 2;
            string? semanticName = isFk
                ? field.Name[..^2]   // strip trailing "Id"
                : null;

            entity.Properties.Add(new OntologyEntityProperty
            {
                Name          = field.Name,
                Labels        = ToLabels(field.Display),
                Comment       = field.Desc?.Key,
                RangeIri      = rangeIri,
                Kind          = kind,
                IsRequired    = field.Require == true,
                IsMultiValued = isMulti,
                IsForeignKey  = isFk,
                SemanticName  = semanticName,
            });
        }

        graph.EntityClasses.Add(entity);
    }

    #endregion

    // -------------------------------------------------------------------------
    #region Enum class builder

    private static string BuildEnumClass(
        SchemaContext context,
        OntologyGraph graph,
        EnumType enumType,
        HashSet<string> visitedEnums)
    {
        string seg = Seg(enumType.Name);
        string iri = $"{graph.EnumPrefix}{seg}";

        if (visitedEnums.Add(enumType.Name))
        {
            graph.EnumClasses.Add(new OntologyEnumClass
            {
                Name   = seg,
                Iri    = iri,
                Labels = ToLabels(enumType.Display),
                Values = enumType.LoadEnumSubListAsync(context, "").GetAwaiter().GetResult()
                    .Where(v => !string.IsNullOrEmpty(v.Value))
                    .Select(v => new OntologyEnumValue
                    {
                        Value  = v.Value,
                        Labels = ToLabels(v.Name),
                    })
                    .ToArray() ?? [],
            });
        }

        return $"en:{seg}";
    }

    #endregion

    // -------------------------------------------------------------------------
    #region Helpers

    /// <summary>
    /// Converts a <see cref="LocaleString"/> to an array of <see cref="OntologyLabel"/>s.
    /// The default key is emitted without a language tag; each <c>Trans</c> entry carries its tag.
    /// </summary>
    private static OntologyLabel[] ToLabels(LocaleString? locale)
    {
        if (locale == null || string.IsNullOrEmpty(locale.Key)) return [];

        var labels = new List<OntologyLabel> { new() { Value = locale.Key } };
        if (locale.Trans != null)
        {
            foreach (LocaleTran t in locale.Trans)
            {
                if (!string.IsNullOrEmpty(t.Tran))
                    labels.Add(new OntologyLabel { Value = t.Tran, Language = t.Lang });
            }
        }

        return [.. labels];
    }

    private static string ScalarToXsd(string name) => name.ToLowerInvariant() switch
    {
        NS_SYSTEM_BOOL                                                       => "xsd:boolean",
        NS_SYSTEM_INT                                                        => "xsd:integer",
        NS_SYSTEM_NUMBER or NS_SYSTEM_DOUBLE or NS_SYSTEM_FLOAT
            or NS_SYSTEM_PERCENT                                             => "xsd:decimal",
        NS_SYSTEM_DATE or NS_SYSTEM_YEAR or NS_SYSTEM_YEARMONTH              => "xsd:date",
        NS_SYSTEM_FULL_DATE                                                  => "xsd:dateTime",
        NS_SYSTEM_RANGE_DATE or NS_SYSTEM_RANGE_FULL_DATE
            or NS_SYSTEM_RANGE_MONTH or NS_SYSTEM_RANGE_YEAR                => "xsd:string",
        NS_SYSTEM_GUID                                                       => "xsd:string",
        _                                                                    => "xsd:string",
    };

    /// <summary>
    /// Post-pass: resolves <see cref="OntologyEntityProperty.SemanticRangeIri"/> for every FK property.
    /// Tries to match the semantic name against known entity class names in the graph;
    /// falls back to <c>owl:Thing</c> when no match is found.
    /// </summary>
    private static void ResolveFkRanges(OntologyGraph graph)
    {
        foreach (OntologyEntityClass entity in graph.EntityClasses)
        {
            foreach (OntologyEntityProperty prop in entity.Properties)
            {
                if (!prop.IsForeignKey || prop.SemanticName == null) continue;

                OntologyEntityClass? target = graph.EntityClasses.FirstOrDefault(e =>
                    e.Name.Equals(prop.SemanticName, StringComparison.OrdinalIgnoreCase)
                    || e.Name.EndsWith('_' + prop.SemanticName, StringComparison.OrdinalIgnoreCase));

                prop.SemanticRangeIri = target != null ? $"app:{target.Name}" : "owl:Thing";
            }
        }
    }

    /// <summary>Returns a Turtle-safe local-name segment (dots and spaces → underscores).</summary>
    private static string Seg(string name) => name.Replace('.', '_').Replace(' ', '_');

    #endregion
}
