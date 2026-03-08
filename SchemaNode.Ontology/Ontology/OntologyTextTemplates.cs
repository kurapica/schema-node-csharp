using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SchemaNode.Ontology;

/// <summary>
/// Built-in text templates for rendering an <see cref="OntologyGraph"/> to four formats:
/// <c>turtle</c> (OWL/Turtle RDF), <c>markdown</c>, <c>jsonld</c>, or <c>ssp</c>
/// (Semantic Schema Projection — machine-optimised plain text for vector generation).
/// <para>
/// Every format is organised in the same semantic layers:
/// <list type="number">
///   <item><b>§0 Scope</b>           — Abstract context-ownership entity classes.</item>
///   <item><b>§1 Vocabulary</b>       — SKOS ConceptSchemes for all enum types.</item>
///   <item><b>§2 Domain</b>           — <c>owl:Class</c> + <c>owl:DatatypeProperty</c> / <c>owl:ObjectProperty</c>,
///                                      including inferred semantic properties for FK fields.</item>
///   <item><b>§3 Infrastructure</b>   — App domain containers, <c>owl:AnnotationProperty</c> data-table annotations.</item>
/// </list>
/// </para>
/// </summary>
public static class OntologyTextTemplates
{
    /// <summary>OWL/Turtle RDF (default).</summary>
    public const string FormatTurtle   = "turtle";

    /// <summary>Human-readable Markdown documentation.</summary>
    public const string FormatMarkdown = "markdown";

    /// <summary>JSON-LD.</summary>
    public const string FormatJsonLd   = "jsonld";

    /// <summary>
    /// Semantic Schema Projection (SSP-v1) — machine-optimised plain text, one block per schema,
    /// designed as the primary input for vector embedding and AI reasoning over this ontology.
    /// </summary>
    public const string FormatSsp = "ssp";

    /// <summary>Renders <paramref name="graph"/> to the specified <paramref name="format"/>.</summary>
    /// <param name="graph">The ontology graph to render.</param>
    /// <param name="format">Output format identifier (see <c>Format*</c> constants).</param>
    /// <param name="locale">
    /// Language tag used to select labels in SSP output (e.g. <c>"enUS"</c>, <c>"zhCN"</c>).
    /// When <see langword="null"/> the default label key is used.
    /// Only affects the <see cref="FormatSsp"/> format; other formats always include all language labels.
    /// </param>
    public static string Render(OntologyGraph graph, string format = FormatTurtle, string? locale = null) =>
        format.ToLowerInvariant() switch
        {
            FormatMarkdown => RenderMarkdown(graph),
            FormatJsonLd   => RenderJsonLd(graph),
            FormatSsp      => RenderSsp(graph, locale),
            _              => RenderTurtle(graph),
        };

    // =========================================================================
    #region Turtle

    private static string RenderTurtle(OntologyGraph graph)
    {
        var sb = new StringBuilder();

        // Prefixes
        sb.AppendLine("@prefix owl:    <http://www.w3.org/2002/07/owl#> .");
        sb.AppendLine("@prefix rdf:    <http://www.w3.org/1999/02/22-rdf-syntax-ns#> .");
        sb.AppendLine("@prefix rdfs:   <http://www.w3.org/2000/01/rdf-schema#> .");
        sb.AppendLine("@prefix xsd:    <http://www.w3.org/2001/XMLSchema#> .");
        sb.AppendLine("@prefix skos:   <http://www.w3.org/2004/02/skos/core#> .");
        sb.AppendLine("@prefix schema: <https://schema.org/> .");
        sb.AppendLine($"@prefix app:    <{graph.AppPrefix}> .");
        sb.AppendLine($"@prefix prop:   <{graph.PropPrefix}> .");
        sb.AppendLine($"@prefix en:     <{graph.EnumPrefix}> .");
        sb.AppendLine($"@prefix ont:    <{graph.OntPrefix}> .");
        sb.AppendLine();
        sb.AppendLine($"<{graph.BaseUri}> a owl:Ontology ;");
        sb.AppendLine($"    rdfs:label \"{Esc(graph.AppName)} Ontology\" .");
        sb.AppendLine();

        // Fix 3: declare all ont: annotation properties so the ont: vocabulary is self-contained
        sb.AppendLine("ont:multiValued    a owl:AnnotationProperty .");
        sb.AppendLine("ont:isDataTable    a owl:AnnotationProperty .");
        sb.AppendLine("ont:valueOrigin    a owl:AnnotationProperty .");
        sb.AppendLine("ont:returnType     a owl:AnnotationProperty .");
        sb.AppendLine("ont:isPure         a owl:AnnotationProperty .");
        sb.AppendLine("ont:isConverter    a owl:AnnotationProperty .");
        sb.AppendLine("ont:isWorkflowOnly a owl:AnnotationProperty .");
        sb.AppendLine("ont:hasSideEffect  a owl:AnnotationProperty .");
        sb.AppendLine();

        // =================================================================
        sb.AppendLine("# =================================================================");
        sb.AppendLine("# §0  SCOPE LAYER  -  Context ownership entities (abstract owl:Class)");
        sb.AppendLine("# =================================================================");
        sb.AppendLine();

        if (graph.ContextEntityClasses.Count > 0)
        {
            foreach (OntologyContextEntityClass ce in graph.ContextEntityClasses)
            {
                sb.Append($"app:{ce.Name} a owl:Class");
                if (!string.IsNullOrEmpty(ce.Comment))
                    sb.Append($" ;\n    rdfs:comment \"{Esc(ce.Comment)}\"");
                sb.AppendLine(" .");
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("# (no context-entity classes in this schema)");
            sb.AppendLine();
        }

        // =================================================================
        sb.AppendLine("# =================================================================");
        sb.AppendLine("# §1  VOCABULARY LAYER  -  Controlled vocabularies (SKOS)");
        sb.AppendLine("# =================================================================");
        sb.AppendLine();

        if (graph.EnumClasses.Count > 0)
        {
            foreach (OntologyEnumClass ec in graph.EnumClasses)
            {
                // ConceptScheme declaration — also typed owl:Class so it is valid as rdfs:range
                sb.Append($"en:{ec.Name} a skos:ConceptScheme , owl:Class");
                AppendTurtleLabels(sb, ec.Labels);
                sb.AppendLine(" .");
                sb.AppendLine();

                // One skos:Concept per enum value
                foreach (OntologyEnumValue v in ec.Values)
                {
                    string conceptLocal = $"{ec.Name}.{Seg(v.Value)}";
                    sb.Append($"en:{conceptLocal} a skos:Concept");
                    sb.Append($" ;\n    skos:inScheme en:{ec.Name}");
                    sb.Append($" ;\n    skos:notation \"{Esc(v.Value)}\"^^xsd:string");

                    if (v.Labels.Length > 0)
                    {
                        foreach (OntologyLabel lbl in v.Labels)
                            sb.Append(lbl.Language != null
                                ? $" ;\n    skos:prefLabel \"{Esc(lbl.Value)}\"@{NormalizeLangTag(lbl.Language)}"
                                : $" ;\n    skos:prefLabel \"{Esc(lbl.Value)}\"");
                    }
                    else
                    {
                        sb.Append($" ;\n    skos:prefLabel \"{Esc(v.Value)}\"");
                    }
                    sb.AppendLine(" .");
                    sb.AppendLine();
                }
            }
        }
        else
        {
            sb.AppendLine("# (no enum types in this schema)");
            sb.AppendLine();
        }

        // =================================================================
        sb.AppendLine("# =================================================================");
        sb.AppendLine("# §2  DOMAIN LAYER  -  Entity classes and semantic properties");
        sb.AppendLine("# =================================================================");
        sb.AppendLine();

        foreach (OntologyEntityClass ec in graph.EntityClasses)
        {
            // Class declaration with optional inheritance
            sb.Append($"app:{ec.Name} a owl:Class");
            if (!string.IsNullOrEmpty(ec.BaseClassIri))
                sb.Append($" ;\n    rdfs:subClassOf <{ec.BaseClassIri}>");
            AppendTurtleLabels(sb, ec.Labels);
            sb.AppendLine(" .");
            sb.AppendLine();

            foreach (OntologyEntityProperty p in ec.Properties)
            {
                // Principle 2/3: DatatypeProperty for literals, ObjectProperty for individuals
                string rdfType = p.Kind == OntologyPropertyKind.Object
                    ? "owl:ObjectProperty"
                    : "owl:DatatypeProperty";

                sb.Append($"prop:{ec.Name}.{p.Name} a {rdfType}");
                sb.Append($" ;\n    rdfs:domain app:{ec.Name}");
                sb.Append($" ;\n    rdfs:range {p.RangeIri}");
                AppendTurtleLabels(sb, p.Labels);
                if (!string.IsNullOrEmpty(p.Comment))
                    sb.Append($" ;\n    rdfs:comment \"{Esc(p.Comment)}\"");
                // Principle 7: multiValued annotation (cardinality axiom is emitted as owl:Restriction below)
                if (p.IsMultiValued)
                    sb.Append(" ;\n    ont:multiValued \"true\"^^xsd:boolean");
                // Principle 5: FK annotation
                if (p.IsForeignKey)
                    sb.Append($" ;\n    rdfs:seeAlso prop:{ec.Name}.{p.SemanticName}");
                sb.AppendLine(" .");
                sb.AppendLine();

                // Principle 5: inferred owl:ObjectProperty from FK field
                if (p.IsForeignKey && !string.IsNullOrEmpty(p.SemanticName))
                {
                    sb.Append($"prop:{ec.Name}.{p.SemanticName} a owl:ObjectProperty");
                    sb.Append($" ;\n    rdfs:domain app:{ec.Name}");
                    // Fix 2: emit rdfs:range resolved by the post-pass (owl:Thing when unresolved)
                    if (!string.IsNullOrEmpty(p.SemanticRangeIri))
                        sb.Append($" ;\n    rdfs:range {p.SemanticRangeIri}");
                    sb.Append($" ;\n    rdfs:comment \"[semantic relation inferred from FK: {p.Name}]\"");
                    sb.AppendLine(" .");
                    sb.AppendLine();
                }
            }

            // Fix 4: owl:minCardinality belongs inside an owl:Restriction subclass axiom, not on a property
            foreach (OntologyEntityProperty req in ec.Properties.Where(p => p.IsRequired))
            {
                sb.AppendLine($"app:{ec.Name} rdfs:subClassOf [");
                sb.AppendLine($"    a owl:Restriction ;");
                sb.AppendLine($"    owl:onProperty prop:{ec.Name}.{req.Name} ;");
                sb.AppendLine($"    owl:minCardinality \"1\"^^xsd:nonNegativeInteger");
                sb.AppendLine($"] .");
                sb.AppendLine();
            }
        }

        if (graph.EntityClasses.Count == 0)
        {
            sb.AppendLine("# (no entity classes in this schema)");
            sb.AppendLine();
        }

        // Scalar types
        foreach (OntologyScalarClass sc in graph.ScalarClasses)
        {
            sb.Append($"app:{sc.Name} a rdfs:Datatype");
            AppendTurtleLabels(sb, sc.Labels);
            if (!string.IsNullOrEmpty(sc.BaseType))
            {
                sb.Append($" ;\n    owl:onDatatype {sc.BaseType}");
                bool hasFacets = sc.LowLimit.HasValue || sc.UpLimit.HasValue
                              || !string.IsNullOrEmpty(sc.Regex);
                if (hasFacets)
                {
                    sb.Append(" ;\n    owl:withRestrictions (");
                    if (sc.LowLimit.HasValue)
                        sb.Append($" [ xsd:minInclusive \"{sc.LowLimit.Value}\"^^xsd:decimal ]");
                    if (sc.UpLimit.HasValue)
                        sb.Append($" [ xsd:maxInclusive \"{sc.UpLimit.Value}\"^^xsd:decimal ]");
                    if (!string.IsNullOrEmpty(sc.Regex))
                        sb.Append($" [ xsd:pattern \"{Esc(sc.Regex)}\" ]");
                    sb.Append(" )");
                }
            }
            if (!string.IsNullOrEmpty(sc.Unit))
                sb.Append($" ;\n    schema:unitText \"{Esc(sc.Unit)}\"");
            sb.AppendLine(" .");
            sb.AppendLine();
        }

        // Function types
        foreach (OntologyFunctionClass ft in graph.FunctionClasses)
        {
            sb.Append($"prop:{ft.Name} a owl:AnnotationProperty");
            AppendTurtleLabels(sb, ft.Labels);
            sb.Append($" ;\n    ont:returnType \"{Esc(ft.ReturnTypeStr)}\"");
            if (ft.IsPure)        sb.Append(" ;\n    ont:isPure \"true\"^^xsd:boolean");
            if (ft.IsConverter)   sb.Append(" ;\n    ont:isConverter \"true\"^^xsd:boolean");
            if (ft.IsWorkflowOnly) sb.Append(" ;\n    ont:isWorkflowOnly \"true\"^^xsd:boolean");
            if (ft.HasSideEffect)  sb.Append(" ;\n    ont:hasSideEffect \"true\"^^xsd:boolean");
            sb.AppendLine(" .");
            foreach (OntologyFunctionArg a in ft.Args)
            {
                string nullable = a.IsNullable ? " nullable" : "";
                string parms    = a.IsParams   ? " params"   : "";
                sb.AppendLine($"# arg {a.Name}: {a.TypeStr}{nullable}{parms}");
            }
            sb.AppendLine();
        }

        // =================================================================
        sb.AppendLine("# =================================================================");
        sb.AppendLine("# §3  INFRASTRUCTURE LAYER  -  App domain containers and data tables");
        sb.AppendLine("# =================================================================");
        sb.AppendLine();

        foreach (OntologyAppClass cls in graph.AppClasses)
        {
            sb.Append($"app:{cls.Name} a owl:Class");
            if (!string.IsNullOrEmpty(cls.ParentIri))
                sb.Append($" ;\n    rdfs:subClassOf <{cls.ParentIri}>");
            AppendTurtleLabels(sb, cls.Labels);
            if (!string.IsNullOrEmpty(cls.Comment))
                sb.Append($" ;\n    rdfs:comment \"{Esc(cls.Comment)}\"");
            sb.AppendLine(" .");
            sb.AppendLine();

            foreach (OntologyTableField t in cls.Tables)
            {
                sb.Append($"prop:{cls.Name}.{t.Name} a owl:AnnotationProperty");
                AppendTurtleLabels(sb, t.Labels);
                sb.Append($" ;\n    rdfs:domain app:{cls.Name}");
                sb.Append($" ;\n    rdfs:range {t.RangeIri}");
                if (!string.IsNullOrEmpty(t.Comment))
                    sb.Append($" ;\n    rdfs:comment \"{Esc(t.Comment)}\"");
                sb.Append(" ;\n    ont:isDataTable \"true\"^^xsd:boolean");
                if (t.IsMultiValued) sb.Append(" ;\n    ont:multiValued \"true\"^^xsd:boolean");
                if (t.IsComputed)   sb.Append(" ;\n    ont:valueOrigin \"computed\"");
                sb.AppendLine(" .");
                sb.AppendLine();
            }

            // Scope relations: forward + inverse ObjectProperty pairs
            foreach (OntologyScopeRelation sr in cls.ScopeRelations)
            {
                string kindComment = sr.Kind switch
                {
                    OntologyScopeRelationKind.Composition => "composition",
                    OntologyScopeRelationKind.Aggregation => "aggregation",
                    _                                     => "association",
                };
                if (!string.IsNullOrEmpty(sr.ContextItem))
                    sb.AppendLine($"# scope:{kindComment} derived from '{sr.ContextItem}'");

                    // Fix 5: use prefixed IRI form for domain to be consistent with the rest of the output
                    string domainPrefixed = sr.DomainIri.StartsWith(graph.AppPrefix, StringComparison.Ordinal)
                        ? $"app:{sr.DomainIri[graph.AppPrefix.Length..]}"
                        : $"<{sr.DomainIri}>";

                    // Forward property
                    sb.Append($"{sr.ForwardProperty} a owl:ObjectProperty");
                    if (sr.IsFunctional) sb.Append(" , owl:FunctionalProperty");
                    sb.Append($" ;\n    rdfs:domain {domainPrefixed}");
                    sb.Append($" ;\n    rdfs:range {sr.RangeIri}");
                    sb.Append($" ;\n    owl:inverseOf {sr.InverseProperty}");
                    if (!string.IsNullOrEmpty(sr.SubPropertyOf))
                        sb.Append($" ;\n    rdfs:subPropertyOf {sr.SubPropertyOf}");
                    sb.AppendLine(" .");
                    sb.AppendLine();

                    // Inverse property
                    sb.Append($"{sr.InverseProperty} a owl:ObjectProperty");
                    if (sr.IsFunctional) sb.Append(" , owl:InverseFunctionalProperty");
                    sb.Append($" ;\n    rdfs:domain {sr.RangeIri}");
                    sb.Append($" ;\n    rdfs:range {domainPrefixed}");
                    sb.Append($" ;\n    owl:inverseOf {sr.ForwardProperty}");
                    if (!string.IsNullOrEmpty(sr.SubPropertyOf))
                        sb.Append($" ;\n    rdfs:subPropertyOf schema:hasPart");
                    sb.AppendLine(" .");
                    sb.AppendLine();

                    // Fix 4: owl:minCardinality as a proper class restriction on the domain (not on the property)
                    if (!sr.IsFunctional)
                    {
                        sb.AppendLine($"{domainPrefixed} rdfs:subClassOf [");
                        sb.AppendLine($"    a owl:Restriction ;");
                        sb.AppendLine($"    owl:onProperty {sr.ForwardProperty} ;");
                        sb.AppendLine($"    owl:minCardinality \"1\"^^xsd:nonNegativeInteger");
                        sb.AppendLine($"] .");
                        sb.AppendLine();
                    }
            }
        }

        // Relation annotations
        foreach (OntologyAppClass cls in graph.AppClasses.Where(c => c.Relations.Count > 0))
        {
            foreach (OntologyRelation r in cls.Relations)
            {
                string args = string.Join(", ", r.Args.Select(a => $"\"{Esc(a)}\""));
                sb.AppendLine($"# {cls.Name}.{r.Field}: {r.RelationType} via {r.Function}({args})");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void AppendTurtleLabels(StringBuilder sb, OntologyLabel[] labels)
    {
        foreach (OntologyLabel lbl in labels)
            sb.Append(lbl.Language != null
                ? $" ;\n    rdfs:label \"{Esc(lbl.Value)}\"@{NormalizeLangTag(lbl.Language)}"
                : $" ;\n    rdfs:label \"{Esc(lbl.Value)}\"");
    }

    /// <summary>
    /// Normalises a language tag to BCP-47 format by inserting a hyphen at the
    /// lower-to-upper-case boundary (e.g. <c>enUS</c> → <c>en-US</c>, <c>zhCN</c> → <c>zh-CN</c>).
    /// Tags that already contain a separator are returned unchanged.
    /// </summary>
    private static string NormalizeLangTag(string tag)
    {
        if (tag.Contains('-') || tag.Contains('_')) return tag;
        for (int i = 1; i < tag.Length; i++)
        {
            if (char.IsLower(tag[i - 1]) && char.IsUpper(tag[i]))
                return tag[..i] + '-' + tag[i..];
        }
        return tag;
    }

    private static string Esc(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");

    #endregion

    // =========================================================================
    #region Markdown

    private static string RenderMarkdown(OntologyGraph graph)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# Ontology: {graph.AppName}");
        sb.AppendLine();
        sb.AppendLine($"**Base IRI**: `{graph.BaseUri}`");
        sb.AppendLine();

        // =================================================================
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## \u00a70  Scope Layer");
        sb.AppendLine();
        sb.AppendLine("Abstract context-ownership entity classes referenced by scope-relation ObjectProperties.");
        sb.AppendLine();

        if (graph.ContextEntityClasses.Count > 0)
        {
            sb.AppendLine("| Class | IRI | Role |");
            sb.AppendLine("|-------|-----|------|");
            foreach (OntologyContextEntityClass ce in graph.ContextEntityClasses)
                sb.AppendLine($"| `{ce.Name}` | `app:{ce.Name}` | {ce.Comment ?? ""} |");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("*(no context-entity classes)*");
            sb.AppendLine();
        }

        // =================================================================
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## \u00a71  Vocabulary Layer");
        sb.AppendLine();
        sb.AppendLine("Controlled vocabularies for all enumeration types, expressed as SKOS ConceptSchemes.");
        sb.AppendLine();

        if (graph.EnumClasses.Count > 0)
        {
            foreach (OntologyEnumClass ec in graph.EnumClasses)
            {
                sb.AppendLine($"### {ec.Name}");
                sb.AppendLine();
                sb.AppendLine($"**IRI**: `en:{ec.Name}`  **Type**: `skos:ConceptScheme`");
                if (ec.Labels.Length > 0) sb.AppendLine($"**Label**: {FmtLabels(ec.Labels)}");
                sb.AppendLine();

                if (ec.Values.Length > 0)
                {
                    sb.AppendLine("| Value | Label(s) | Concept IRI |");
                    sb.AppendLine("|-------|---------|-------------|");
                    foreach (OntologyEnumValue v in ec.Values)
                        sb.AppendLine($"| `{v.Value}` | {FmtLabels(v.Labels)} | `en:{ec.Name}.{Seg(v.Value)}` |");
                    sb.AppendLine();
                }
            }
        }
        else
        {
            sb.AppendLine("*(no enum types)*");
            sb.AppendLine();
        }

        // =================================================================
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## \u00a72  Domain Layer");
        sb.AppendLine();
        sb.AppendLine("Entity classes (`owl:Class`) and their semantic properties (`owl:DatatypeProperty` / `owl:ObjectProperty`).");
        sb.AppendLine();

        foreach (OntologyEntityClass ec in graph.EntityClasses)
        {
            sb.AppendLine($"### {ec.Name}");
            sb.AppendLine();
            sb.AppendLine($"**IRI**: `app:{ec.Name}`  **Type**: `owl:Class`");
            if (ec.Labels.Length > 0) sb.AppendLine($"**Label**: {FmtLabels(ec.Labels)}");
            if (!string.IsNullOrEmpty(ec.BaseClassIri)) sb.AppendLine($"**Inherits**: `{ec.BaseClassIri}`");
            sb.AppendLine();

            if (ec.Properties.Count > 0)
            {
                sb.AppendLine("| Field | Label(s) | OWL Type | Range | Required | Multi | FK\u2192Semantic |");
                sb.AppendLine("|-------|---------|----------|-------|:--------:|:-----:|-------------|");
                foreach (OntologyEntityProperty p in ec.Properties)
                {
                    string kind = p.Kind == OntologyPropertyKind.Object ? "ObjectProperty" : "DatatypeProperty";
                    string fk   = p.IsForeignKey ? $"`prop:{ec.Name}.{p.SemanticName}`" : "";
                    sb.AppendLine(
                        $"| `{p.Name}` | {FmtLabels(p.Labels)} | {kind} | `{p.RangeIri}` " +
                        $"| {(p.IsRequired ? "\u2713" : "")} | {(p.IsMultiValued ? "\u2713" : "")} | {fk} |");
                }
                sb.AppendLine();

                var fkProps = ec.Properties.Where(p => p.IsForeignKey).ToList();
                if (fkProps.Count > 0)
                {
                    sb.AppendLine("> **Inferred semantic ObjectProperties (Principle 5):**");
                    foreach (OntologyEntityProperty p in fkProps)
                        sb.AppendLine($">  - `prop:{ec.Name}.{p.SemanticName}` \u2014 semantic relation inferred from FK field `{p.Name}`");
                    sb.AppendLine();
                }
            }
        }

        if (graph.EntityClasses.Count == 0)
        {
            sb.AppendLine("*(no entity classes)*");
            sb.AppendLine();
        }

        // Scalar types
        if (graph.ScalarClasses.Count > 0)
        {
            sb.AppendLine("### Scalar Types");
            sb.AppendLine();
            sb.AppendLine("| Name | Label(s) | BaseType | LowLimit | UpLimit | Unit | Regex |");
            sb.AppendLine("|------|---------|---------|:--------:|:-------:|------|-------|");
            foreach (OntologyScalarClass sc in graph.ScalarClasses)
            {
                sb.AppendLine(
                    $"| `{sc.Name}` | {FmtLabels(sc.Labels)} | `{sc.BaseType ?? "—"}` " +
                    $"| {sc.LowLimit?.ToString() ?? "—"} | {sc.UpLimit?.ToString() ?? "—"} " +
                    $"| {sc.Unit ?? "—"} | {(string.IsNullOrEmpty(sc.Regex) ? "—" : $"`{sc.Regex}`")} |");
            }
            sb.AppendLine();
        }

        // Function types
        if (graph.FunctionClasses.Count > 0)
        {
            foreach (OntologyFunctionClass ft in graph.FunctionClasses)
            {
                sb.AppendLine($"### {ft.Name}");
                sb.AppendLine();
                sb.AppendLine($"**IRI**: `prop:{ft.Name}`  **Type**: Function");
                if (ft.Labels.Length > 0) sb.AppendLine($"**Label**: {FmtLabels(ft.Labels)}");
                sb.AppendLine($"**Returns**: `{ft.ReturnTypeStr}`");
                string flags = string.Join(", ", new[]
                {
                    ft.IsPure         ? "Pure"         : null,
                    ft.IsConverter    ? "Converter"    : null,
                    ft.IsWorkflowOnly ? "WorkflowOnly" : null,
                    ft.HasSideEffect  ? "SideEffect"   : null,
                }.Where(f => f != null));
                if (!string.IsNullOrEmpty(flags)) sb.AppendLine($"**Flags**: {flags}");
                sb.AppendLine();

                if (ft.Args.Length > 0)
                {
                    sb.AppendLine("| Arg | Type | Label(s) | Nullable | Params |");
                    sb.AppendLine("|-----|------|---------|:--------:|:------:|");
                    foreach (OntologyFunctionArg a in ft.Args)
                        sb.AppendLine(
                            $"| `{a.Name}` | `{a.TypeStr}` | {FmtLabels(a.Labels)} " +
                            $"| {(a.IsNullable ? "\u2713" : "")} | {(a.IsParams ? "\u2713" : "")} |");
                    sb.AppendLine();
                }
            }
        }

        // =================================================================
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## \u00a73  Infrastructure Layer");
        sb.AppendLine();
        sb.AppendLine("App domain containers and data-table field annotations (`owl:AnnotationProperty`).");
        sb.AppendLine();

        foreach (OntologyAppClass cls in graph.AppClasses)
        {
            sb.AppendLine($"### {cls.Name}");
            sb.AppendLine();
            sb.AppendLine($"**IRI**: `app:{cls.Name}`  **Type**: `owl:Class`");
            if (cls.Labels.Length > 0)    sb.AppendLine($"**Label**: {FmtLabels(cls.Labels)}");
            if (!string.IsNullOrEmpty(cls.Comment)) sb.AppendLine($"**Description**: {cls.Comment}");
            if (!string.IsNullOrEmpty(cls.ParentIri)) sb.AppendLine($"**SubClassOf**: `{cls.ParentIri}`");
            sb.AppendLine();

            if (cls.Tables.Count > 0)
            {
                sb.AppendLine("| Table Field | Label(s) | Entity Class | Multi | Computed |");
                sb.AppendLine("|------------|---------|-------------|:-----:|:--------:|");
                foreach (OntologyTableField t in cls.Tables)
                    sb.AppendLine($"| `{t.Name}` | {FmtLabels(t.Labels)} | `{t.RangeIri}` | {(t.IsMultiValued ? "\u2713" : "")} | {(t.IsComputed ? "\u2713" : "")} |");
                sb.AppendLine();
            }

            if (cls.Relations.Count > 0)
            {
                sb.AppendLine("**Field relations:**");
                sb.AppendLine();
                sb.AppendLine("| Field | Relation | Function | Arguments |");
                sb.AppendLine("|-------|----------|----------|-----------|");
                foreach (OntologyRelation r in cls.Relations)
                    sb.AppendLine($"| `{r.Field}` | {r.RelationType} | `{r.Function}` | {string.Join(", ", r.Args.Select(a => $"`{a}`"))} |");
                sb.AppendLine();
            }

            if (cls.ScopeRelations.Count > 0)
            {
                sb.AppendLine("**Scope relations (ownership / context-scoping):**");
                sb.AppendLine();
                sb.AppendLine("| Forward property | Inverse property | Range | Kind | Functional | SubPropertyOf |");
                sb.AppendLine("|-----------------|-----------------|-------|------|:----------:|---------------|");
                foreach (OntologyScopeRelation sr in cls.ScopeRelations)
                {
                    string kindLabel = sr.Kind switch
                    {
                        OntologyScopeRelationKind.Composition => "Composition",
                        OntologyScopeRelationKind.Aggregation => "Aggregation",
                        _                                     => "Association",
                    };
                    sb.AppendLine(
                        $"| `{sr.ForwardProperty}` | `{sr.InverseProperty}` | `{sr.RangeIri}` " +
                        $"| {kindLabel} | {(sr.IsFunctional ? "\u2713" : "")} | {sr.SubPropertyOf ?? "—"} |");
                }
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static string FmtLabels(OntologyLabel[] labels)
    {
        if (labels.Length == 0) return "";
        return string.Join(" / ", labels.Select(l =>
            l.Language != null ? $"{l.Value} `({l.Language})`" : l.Value));
    }

    #endregion

    // =========================================================================
    #region JSON-LD

    private static string RenderJsonLd(OntologyGraph graph)
    {
        var ctx = new JsonObject
        {
            ["owl"]    = "http://www.w3.org/2002/07/owl#",
            ["rdf"]    = "http://www.w3.org/1999/02/22-rdf-syntax-ns#",
            ["rdfs"]   = "http://www.w3.org/2000/01/rdf-schema#",
            ["xsd"]    = "http://www.w3.org/2001/XMLSchema#",
            ["skos"]   = "http://www.w3.org/2004/02/skos/core#",
            ["schema"] = "https://schema.org/",
            ["app"]    = graph.AppPrefix,
            ["prop"]   = graph.PropPrefix,
            ["en"]     = graph.EnumPrefix,
        };

        var arr = new JsonArray();

        // Ontology node
        arr.Add(new JsonObject
        {
            ["@id"]        = graph.BaseUri,
            ["@type"]      = "owl:Ontology",
            ["rdfs:label"] = $"{graph.AppName} Ontology",
        });

        // §0 Scope: abstract context-entity classes
        foreach (OntologyContextEntityClass ce in graph.ContextEntityClasses)
        {
            var ceNode = new JsonObject { ["@id"] = $"app:{ce.Name}", ["@type"] = "owl:Class" };
            if (!string.IsNullOrEmpty(ce.Comment)) ceNode["rdfs:comment"] = ce.Comment;
            arr.Add(ceNode);
        }

        // §1 Vocabulary: SKOS ConceptSchemes and Concepts
        foreach (OntologyEnumClass ec in graph.EnumClasses)
        {
            var scheme = new JsonObject { ["@id"] = $"en:{ec.Name}", ["@type"] = "skos:ConceptScheme" };
            SetJsonLdLabels(scheme, ec.Labels, "rdfs:label");
            arr.Add(scheme);

            foreach (OntologyEnumValue v in ec.Values)
            {
                string conceptId = $"en:{ec.Name}.{Seg(v.Value)}";
                var concept = new JsonObject
                {
                    ["@id"]           = conceptId,
                    ["@type"]         = "skos:Concept",
                    ["skos:inScheme"] = new JsonObject { ["@id"] = $"en:{ec.Name}" },
                    ["skos:notation"] = v.Value,
                };
                SetJsonLdLabels(concept, v.Labels, "skos:prefLabel");
                if (concept["skos:prefLabel"] == null) concept["skos:prefLabel"] = v.Value;
                arr.Add(concept);
            }
        }

        // §2 Domain: entity classes and properties
        foreach (OntologyEntityClass ec in graph.EntityClasses)
        {
            var node = new JsonObject { ["@id"] = $"app:{ec.Name}", ["@type"] = "owl:Class" };
            if (!string.IsNullOrEmpty(ec.BaseClassIri))
                node["rdfs:subClassOf"] = new JsonObject { ["@id"] = ec.BaseClassIri };
            SetJsonLdLabels(node, ec.Labels, "rdfs:label");
            arr.Add(node);

            foreach (OntologyEntityProperty p in ec.Properties)
            {
                string rdfType = p.Kind == OntologyPropertyKind.Object
                    ? "owl:ObjectProperty"
                    : "owl:DatatypeProperty";

                var pNode = new JsonObject
                {
                    ["@id"]         = $"prop:{ec.Name}.{p.Name}",
                    ["@type"]       = rdfType,
                    ["rdfs:domain"] = new JsonObject { ["@id"] = $"app:{ec.Name}" },
                    ["rdfs:range"]  = new JsonObject { ["@id"] = Expand(p.RangeIri, graph) },
                };
                SetJsonLdLabels(pNode, p.Labels, "rdfs:label");
                if (!string.IsNullOrEmpty(p.Comment)) pNode["rdfs:comment"]         = p.Comment;
                if (p.IsRequired)    pNode["owl:minCardinality"]   = 1;
                if (p.IsMultiValued) pNode["schema:multiValued"]   = true;
                if (p.IsForeignKey)  pNode["rdfs:seeAlso"]         = $"prop:{ec.Name}.{p.SemanticName}";
                arr.Add(pNode);

                if (p.IsForeignKey && !string.IsNullOrEmpty(p.SemanticName))
                {
                    arr.Add(new JsonObject
                    {
                        ["@id"]          = $"prop:{ec.Name}.{p.SemanticName}",
                        ["@type"]        = "owl:ObjectProperty",
                        ["rdfs:domain"]  = new JsonObject { ["@id"] = $"app:{ec.Name}" },
                        ["rdfs:comment"] = $"Semantic relation inferred from FK field: {p.Name}",
                    });
                }
            }
        }

        // §2 continued: scalar types
        foreach (OntologyScalarClass sc in graph.ScalarClasses)
        {
            var scNode = new JsonObject { ["@id"] = $"app:{sc.Name}", ["@type"] = "rdfs:Datatype" };
            SetJsonLdLabels(scNode, sc.Labels, "rdfs:label");
            if (!string.IsNullOrEmpty(sc.BaseType))
                scNode["owl:onDatatype"] = new JsonObject { ["@id"] = Expand(sc.BaseType, graph) };
            if (sc.LowLimit.HasValue)  scNode["xsd:minInclusive"] = (double)sc.LowLimit.Value;
            if (sc.UpLimit.HasValue)   scNode["xsd:maxInclusive"] = (double)sc.UpLimit.Value;
            if (!string.IsNullOrEmpty(sc.Regex)) scNode["xsd:pattern"] = sc.Regex;
            if (!string.IsNullOrEmpty(sc.Unit))  scNode["schema:unitText"] = sc.Unit;
            arr.Add(scNode);
        }

        // §2 continued: function types
        foreach (OntologyFunctionClass ft in graph.FunctionClasses)
        {
            var ftNode = new JsonObject
            {
                ["@id"]              = $"prop:{ft.Name}",
                ["@type"]            = "owl:AnnotationProperty",
                ["schema:returnType"] = ft.ReturnTypeStr,
            };
            SetJsonLdLabels(ftNode, ft.Labels, "rdfs:label");
            if (ft.IsPure)         ftNode["schema:isPure"]       = true;
            if (ft.IsConverter)    ftNode["schema:isConverter"]  = true;
            if (ft.IsWorkflowOnly) ftNode["schema:workflowOnly"] = true;
            if (ft.HasSideEffect)  ftNode["schema:hasSideEffect"] = true;
            if (ft.Args.Length > 0)
            {
                var argsArr = new JsonArray();
                foreach (OntologyFunctionArg a in ft.Args)
                {
                    var argNode = new JsonObject { ["@value"] = a.Name, ["schema:argType"] = a.TypeStr };
                    if (a.IsNullable) argNode["schema:nullable"] = true;
                    if (a.IsParams)   argNode["schema:params"]   = true;
                    argsArr.Add(argNode);
                }
                ftNode["schema:args"] = argsArr;
            }
            arr.Add(ftNode);
        }

        // §3 Infrastructure: app domain classes and data-table annotations
        foreach (OntologyAppClass cls in graph.AppClasses)
        {
            var node = new JsonObject { ["@id"] = $"app:{cls.Name}", ["@type"] = "owl:Class" };
            if (!string.IsNullOrEmpty(cls.ParentIri))
                node["rdfs:subClassOf"] = new JsonObject { ["@id"] = cls.ParentIri };
            SetJsonLdLabels(node, cls.Labels, "rdfs:label");
            if (!string.IsNullOrEmpty(cls.Comment)) node["rdfs:comment"] = cls.Comment;
            arr.Add(node);

            foreach (OntologyTableField t in cls.Tables)
            {
                var tNode = new JsonObject
                {
                    ["@id"]                = $"prop:{cls.Name}.{t.Name}",
                    ["@type"]              = "owl:AnnotationProperty",
                    ["rdfs:domain"]        = new JsonObject { ["@id"] = $"app:{cls.Name}" },
                    ["rdfs:range"]         = new JsonObject { ["@id"] = Expand(t.RangeIri, graph) },
                    ["schema:isDataTable"] = true,
                };
                SetJsonLdLabels(tNode, t.Labels, "rdfs:label");
                if (!string.IsNullOrEmpty(t.Comment)) tNode["rdfs:comment"] = t.Comment;
                if (t.IsMultiValued) tNode["schema:multiValued"] = true;
                arr.Add(tNode);
            }

            // Scope relations: forward + inverse ObjectProperty pairs
            foreach (OntologyScopeRelation sr in cls.ScopeRelations)
            {
                string[] forwardTypes = sr.IsFunctional
                    ? ["owl:ObjectProperty", "owl:FunctionalProperty"]
                    : ["owl:ObjectProperty"];

                var fwd = new JsonObject
                {
                    ["@id"]          = sr.ForwardProperty,
                    ["@type"]        = sr.IsFunctional ? new JsonArray("owl:ObjectProperty", "owl:FunctionalProperty") : (JsonNode)"owl:ObjectProperty",
                    ["rdfs:domain"]  = new JsonObject { ["@id"] = Expand(sr.DomainIri, graph) },
                    ["rdfs:range"]   = new JsonObject { ["@id"] = Expand(sr.RangeIri, graph) },
                    ["owl:inverseOf"] = new JsonObject { ["@id"] = sr.InverseProperty },
                };
                if (!string.IsNullOrEmpty(sr.SubPropertyOf))
                    fwd["rdfs:subPropertyOf"] = new JsonObject { ["@id"] = sr.SubPropertyOf };
                fwd[sr.IsFunctional ? "owl:cardinality" : "owl:minCardinality"] = 1;
                if (!string.IsNullOrEmpty(sr.ContextItem))
                    fwd["rdfs:comment"] = $"Scope {sr.Kind.ToString().ToLowerInvariant()} derived from '{sr.ContextItem}'";
                arr.Add(fwd);

                var inv = new JsonObject
                {
                    ["@id"]          = sr.InverseProperty,
                    ["@type"]        = sr.IsFunctional ? new JsonArray("owl:ObjectProperty", "owl:InverseFunctionalProperty") : (JsonNode)"owl:ObjectProperty",
                    ["rdfs:domain"]  = new JsonObject { ["@id"] = Expand(sr.RangeIri, graph) },
                    ["rdfs:range"]   = new JsonObject { ["@id"] = Expand(sr.DomainIri, graph) },
                    ["owl:inverseOf"] = new JsonObject { ["@id"] = sr.ForwardProperty },
                };
                if (!string.IsNullOrEmpty(sr.SubPropertyOf))
                    inv["rdfs:subPropertyOf"] = new JsonObject { ["@id"] = "schema:hasPart" };
                arr.Add(inv);
            }
        }

        var root = new JsonObject { ["@context"] = ctx, ["@graph"] = arr };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static void SetJsonLdLabels(JsonObject node, OntologyLabel[] labels, string predicate)
    {
        if (labels.Length == 0) return;
        if (labels.Length == 1 && labels[0].Language == null)
        {
            node[predicate] = labels[0].Value;
            return;
        }
        var arr = new JsonArray();
        foreach (OntologyLabel lbl in labels)
        {
            var entry = new JsonObject { ["@value"] = lbl.Value };
            if (lbl.Language != null) entry["@language"] = lbl.Language;
            arr.Add(entry);
        }
        node[predicate] = arr;
    }

    private static string Expand(string iri, OntologyGraph graph)
    {
        if (iri.StartsWith("app:"))   return graph.AppPrefix  + iri[4..];
        if (iri.StartsWith("en:"))    return graph.EnumPrefix + iri[3..];
        if (iri.StartsWith("xsd:"))   return "http://www.w3.org/2001/XMLSchema#" + iri[4..];
        if (iri.StartsWith("prop:"))  return graph.PropPrefix + iri[5..];
        if (iri.StartsWith("skos:"))  return "http://www.w3.org/2004/02/skos/core#" + iri[5..];
        return iri;
    }

    private static string Seg(string name) => name.Replace('.', '_').Replace(' ', '_');

    #endregion

    // =========================================================================
    #region SSP  (Semantic Schema Projection — SSP-v1)

    /// <summary>
    /// Renders the ontology graph as SSP-v1 plain text: one delimited block per schema,
    /// ordered Vocabulary → Entity → Container, each with typed sections
    /// (Fields / Relations / Behavior / Lifecycle / Ownership / Permissions).
    /// <para>
    /// The format is designed as the primary input for vector embedding and AI reasoning.
    /// Each block is self-contained and cross-references other blocks by local name.
    /// </para>
    /// </summary>
    private static string RenderSsp(OntologyGraph graph, string? locale = null)
    {
        var sb = new StringBuilder();

        // ── Cross-reference lookups ────────────────────────────────────────
        var enumByIri = graph.EnumClasses.ToDictionary(
            e => $"en:{e.Name}", StringComparer.OrdinalIgnoreCase);

        // entity range IRI → list of (AppClass, TableField) that reference it
        var usedInMap = new Dictionary<string, List<(OntologyAppClass App, OntologyTableField Field)>>(
            StringComparer.OrdinalIgnoreCase);
        foreach (OntologyAppClass cls in graph.AppClasses)
        {
            foreach (OntologyTableField t in cls.Tables)
            {
                if (!usedInMap.TryGetValue(t.RangeIri, out var bucket))
                    usedInMap[t.RangeIri] = bucket = [];
                bucket.Add((cls, t));
            }
        }

        // ── Header ─────────────────────────────────────────────────────────
        sb.AppendLine("[SSP-v1]");
        sb.AppendLine($"Ontology: {graph.AppName}");
        sb.AppendLine($"Base: {graph.BaseUri}");

        // ── §0.5 Scalar blocks ─────────────────────────────────────────────
        foreach (OntologyScalarClass sc in graph.ScalarClasses)
        {
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine($"Schema: {sc.Name}");
            sb.AppendLine("Type: Scalar");
            string lbl = SspLabel(sc.Labels, locale);
            if (!string.IsNullOrEmpty(lbl)) sb.AppendLine($"Label: {lbl}");
            if (!string.IsNullOrEmpty(sc.BaseType)) sb.AppendLine($"BaseType: {sc.BaseType}");

            bool hasConstraints = sc.LowLimit.HasValue || sc.UpLimit.HasValue
                               || !string.IsNullOrEmpty(sc.Unit)
                               || !string.IsNullOrEmpty(sc.Regex);
            if (hasConstraints)
            {
                sb.AppendLine();
                sb.AppendLine("Constraints:");
                if (sc.LowLimit.HasValue) sb.AppendLine($"  LowLimit: {sc.LowLimit.Value}");
                if (sc.UpLimit.HasValue)  sb.AppendLine($"  UpLimit: {sc.UpLimit.Value}");
                if (!string.IsNullOrEmpty(sc.Unit))  sb.AppendLine($"  Unit: {sc.Unit}");
                if (!string.IsNullOrEmpty(sc.Regex)) sb.AppendLine($"  Regex: {sc.Regex}");
            }
        }

        // ── §0.6 Function blocks ───────────────────────────────────────────
        foreach (OntologyFunctionClass ft in graph.FunctionClasses)
        {
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine($"Schema: {ft.Name}");
            sb.AppendLine("Type: Function");
            string lbl = SspLabel(ft.Labels, locale);
            if (!string.IsNullOrEmpty(lbl)) sb.AppendLine($"Label: {lbl}");
            sb.AppendLine($"Returns: {ft.ReturnTypeStr}");

            if (ft.Args.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Args:");
                foreach (OntologyFunctionArg a in ft.Args)
                {
                    string nullable = a.IsNullable ? " Nullable" : "";
                    string parms    = a.IsParams   ? " Params"   : "";
                    string aLbl     = SspLabel(a.Labels, locale);
                    string desc     = !string.IsNullOrEmpty(aLbl) ? $" — {aLbl}" : "";
                    sb.AppendLine($"  {a.Name}: {a.TypeStr}{nullable}{parms}{desc}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("Behavior:");
            sb.AppendLine($"  Pure: {ft.IsPure}");
            if (ft.IsConverter)    sb.AppendLine("  Converter: true");
            if (ft.IsWorkflowOnly) sb.AppendLine("  WorkflowOnly: true");
            if (ft.HasSideEffect)  sb.AppendLine("  SideEffect: true");
        }

        // ── §1 Vocabulary blocks ───────────────────────────────────────────
        foreach (OntologyEnumClass ec in graph.EnumClasses)
        {
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine($"Schema: {ec.Name}");
            sb.AppendLine("Type: Vocabulary");
            string lbl = SspLabel(ec.Labels, locale);
            if (!string.IsNullOrEmpty(lbl)) sb.AppendLine($"Label: {lbl}");

            if (ec.Values.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Values:");
                foreach (OntologyEnumValue v in ec.Values)
                {
                    string vLbl = SspLabel(v.Labels, locale);
                    sb.AppendLine(string.IsNullOrEmpty(vLbl)
                        ? $"  {v.Value}"
                        : $"  {v.Value} — {vLbl}");
                }
            }
        }

        // ── §2 Entity blocks ───────────────────────────────────────────────
        foreach (OntologyEntityClass ec in graph.EntityClasses)
        {
            string entityKey = $"app:{ec.Name}";
            usedInMap.TryGetValue(entityKey, out var usages);

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine($"Schema: {ec.Name}");
            sb.AppendLine("Type: Entity");
            string lbl = SspLabel(ec.Labels, locale);
            if (!string.IsNullOrEmpty(lbl)) sb.AppendLine($"Label: {lbl}");
            if (!string.IsNullOrEmpty(ec.BaseClassIri))
                sb.AppendLine($"Extends: {SspLocalName(ec.BaseClassIri)}");

            // Fields
            if (ec.Properties.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Fields:");
                foreach (OntologyEntityProperty p in ec.Properties)
                {
                    string typeStr  = SspTypeStr(p.RangeIri);
                    string required = p.IsRequired    ? " Required" : "";
                    string multi    = p.IsMultiValued ? " Multi"    : "";
                    string pLbl     = SspLabel(p.Labels, locale);
                    string comment  = !string.IsNullOrEmpty(pLbl) ? pLbl
                                    : p.Comment ?? "";
                    string desc = !string.IsNullOrEmpty(comment) ? $" — {comment}" : "";
                    sb.AppendLine($"  {p.Name}: {typeStr}{required}{multi}{desc}");
                }
            }

            // Relations: FK-inferred + container membership
            var fkProps = ec.Properties
                .Where(p => p.IsForeignKey && !string.IsNullOrEmpty(p.SemanticName))
                .ToList();
            if (fkProps.Count > 0 || usages?.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Relations:");
                foreach (OntologyEntityProperty p in fkProps)
                {
                    string pred = $"has{char.ToUpperInvariant(p.SemanticName![0])}{p.SemanticName[1..]}";
                    sb.AppendLine($"  {ec.Name} {pred} [FK: {p.Name}]");
                }
                if (usages != null)
                {
                    foreach (var (app, field) in usages)
                    {
                        string computed = field.IsComputed ? " Computed" : "";
                        sb.AppendLine($"  {ec.Name} isContainedBy {app.Name}.{field.Name}{computed}");
                    }
                }
            }

            // Behavior: tables pointing here that are computed
            if (usages?.Any(u => u.Field.IsComputed) == true)
            {
                sb.AppendLine();
                sb.AppendLine("Behavior:");
                foreach (var (app, field) in usages.Where(u => u.Field.IsComputed))
                    sb.AppendLine($"  Derive: {app.Name}.{field.Name}");
            }

            // Lifecycle: enum-typed fields → state values
            var lifecycleFields = ec.Properties
                .Where(p => p.RangeIri.StartsWith("en:", StringComparison.OrdinalIgnoreCase)
                         && enumByIri.ContainsKey(p.RangeIri))
                .ToList();
            if (lifecycleFields.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Lifecycle:");
                foreach (OntologyEntityProperty p in lifecycleFields)
                {
                    if (enumByIri.TryGetValue(p.RangeIri, out OntologyEnumClass? enumClass))
                    {
                        string states = string.Join(" | ", enumClass.Values.Select(v => v.Value));
                        sb.AppendLine($"  {p.Name}: {enumClass.Name} [{states}]");
                    }
                }
            }
        }

        // ── §3 Container blocks ────────────────────────────────────────────
        foreach (OntologyAppClass cls in graph.AppClasses)
        {
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine($"Schema: {cls.Name}");
            sb.AppendLine("Type: Container");
            string lbl = SspLabel(cls.Labels, locale);
            if (!string.IsNullOrEmpty(lbl))           sb.AppendLine($"Label: {lbl}");
            if (!string.IsNullOrEmpty(cls.Comment))   sb.AppendLine($"Description: {cls.Comment}");
            if (!string.IsNullOrEmpty(cls.ParentIri)) sb.AppendLine($"SubOf: {SspLocalName(cls.ParentIri)}");

            // Tables
            if (cls.Tables.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Tables:");
                foreach (OntologyTableField t in cls.Tables)
                {
                    string typeStr  = SspTypeStr(t.RangeIri);
                    string multi    = t.IsMultiValued ? " Multi"    : "";
                    string computed = t.IsComputed    ? " Computed" : "";
                    string tLbl     = SspLabel(t.Labels, locale);
                    string desc     = !string.IsNullOrEmpty(tLbl) ? $" — {tLbl}" : "";
                    sb.AppendLine($"  {t.Name}: {typeStr}{multi}{computed}{desc}");
                }
            }

            // Relations: contains (inferred from entity tables) + schema-level
            bool hasEntityTables = cls.Tables.Any(t => t.RangeIri.StartsWith("app:", StringComparison.OrdinalIgnoreCase));
            if (hasEntityTables || cls.Relations.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Relations:");
                foreach (OntologyTableField t in cls.Tables
                    .Where(t => t.RangeIri.StartsWith("app:", StringComparison.OrdinalIgnoreCase)))
                {
                    sb.AppendLine($"  {cls.Name} contains {t.RangeIri[4..]}");
                }
                foreach (OntologyRelation r in cls.Relations)
                {
                    string args = r.Args.Length > 0
                        ? $"({string.Join(", ", r.Args)})"
                        : "()";
                    sb.AppendLine($"  {cls.Name}.{r.Field} {r.RelationType} via {r.Function}{args}");
                }
            }

            // Behavior: computed table fields
            var computedTables = cls.Tables.Where(t => t.IsComputed).ToList();
            if (computedTables.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Behavior:");
                foreach (OntologyTableField t in computedTables)
                    sb.AppendLine($"  Derive: {t.Name}");
            }

            // Ownership + Permissions (from scope relations)
            if (cls.ScopeRelations.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Ownership:");
                foreach (OntologyScopeRelation sr in cls.ScopeRelations)
                {
                    string entity = SspLocalName(sr.RangeIri);
                    string kind   = sr.Kind.ToString();
                    string ctx    = !string.IsNullOrEmpty(sr.ContextItem) ? $" [{sr.ContextItem}]" : "";
                    sb.AppendLine($"  {cls.Name} isOwnedBy {entity} ({kind}){ctx}");
                }

                sb.AppendLine();
                sb.AppendLine("Permissions:");
                foreach (OntologyScopeRelation sr in cls.ScopeRelations)
                {
                    string entity = SspLocalName(sr.RangeIri);
                    string rule = sr.Kind switch
                    {
                        OntologyScopeRelationKind.Composition => $"RowAccess: isolated to owning {entity}",
                        OntologyScopeRelationKind.Aggregation => $"RowAccess: scoped by {entity} context",
                        _                                     => $"RowAccess: scoped by {entity} dimension",
                    };
                    sb.AppendLine($"  {rule}");
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("---");
        return sb.ToString();
    }

    /// <summary>
    /// Returns the best label for <paramref name="locale"/>.
    /// When <paramref name="locale"/> is specified the matching translation is preferred;
    /// falls back to the default label (Language == <see langword="null"/>) when no exact contains is found.
    /// Locale normalisation mirrors <c>AccessContextItemProviderExtensions.GetLocaleStringKey</c>:
    /// hyphens are stripped before comparison (e.g. <c>"zh-CN"</c> == <c>"zhCN"</c>).
    /// </summary>
    private static string SspLabel(OntologyLabel[] labels, string? locale = null)
    {
        if (labels.Length == 0) return "";
        if (!string.IsNullOrEmpty(locale))
        {
            string norm = locale.Replace("-", "");
            OntologyLabel? match = labels.FirstOrDefault(l =>
                l.Language != null &&
                l.Language.Replace("-", "").Equals(norm, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match.Value;
        }
        return (labels.FirstOrDefault(l => l.Language == null) ?? labels[0]).Value;
    }

    /// <summary>
    /// Collects every distinct language tag present in the <see cref="OntologyLabel"/> arrays
    /// across the entire graph (i.e. every locale that has at least one translated label).
    /// The default key label (<c>Language == null</c>, stored as <c>"enUS"</c>) is
    /// <b>not</b> included in the returned set.
    /// </summary>
    public static IReadOnlyCollection<string> CollectLocales(OntologyGraph graph)
    {
        var locales = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Scan(OntologyLabel[] labels)
        {
            foreach (OntologyLabel lbl in labels)
                if (lbl.Language != null) locales.Add(lbl.Language);
        }

        foreach (OntologyAppClass cls in graph.AppClasses)
        {
            Scan(cls.Labels);
            foreach (OntologyTableField t in cls.Tables) Scan(t.Labels);
        }
        foreach (OntologyEntityClass ec in graph.EntityClasses)
        {
            Scan(ec.Labels);
            foreach (OntologyEntityProperty p in ec.Properties) Scan(p.Labels);
        }
        foreach (OntologyEnumClass en in graph.EnumClasses)
        {
            Scan(en.Labels);
            foreach (OntologyEnumValue v in en.Values) Scan(v.Labels);
        }
        foreach (OntologyScalarClass sc in graph.ScalarClasses) Scan(sc.Labels);
        foreach (OntologyFunctionClass ft in graph.FunctionClasses)
        {
            Scan(ft.Labels);
            foreach (OntologyFunctionArg a in ft.Args) Scan(a.Labels);
        }

        return locales;
    }

    /// <summary>
    /// Converts a prefixed range IRI to a concise SSP type string.
    /// Scalar XSD types are mapped to title-case names; structured types carry a kind tag.
    /// </summary>
    private static string SspTypeStr(string rangeIri) => rangeIri switch
    {
        "xsd:string"   => "String",
        "xsd:integer"  => "Integer",
        "xsd:decimal"  => "Decimal",
        "xsd:boolean"  => "Boolean",
        "xsd:dateTime" => "DateTime",
        "xsd:date"     => "Date",
        _ when rangeIri.StartsWith("app:", StringComparison.OrdinalIgnoreCase) => $"{rangeIri[4..]} (Entity)",
        _ when rangeIri.StartsWith("en:",  StringComparison.OrdinalIgnoreCase) => $"{rangeIri[3..]} (Enum)",
        _              => rangeIri,
    };

    /// <summary>
    /// Extracts the local name from a prefixed IRI (<c>app:</c>, <c>en:</c>, <c>prop:</c>)
    /// or a full absolute IRI by taking the last path segment after <c>/</c> or <c>#</c>.
    /// </summary>
    private static string SspLocalName(string iri)
    {
        if (iri.StartsWith("app:",  StringComparison.OrdinalIgnoreCase)) return iri[4..];
        if (iri.StartsWith("en:",   StringComparison.OrdinalIgnoreCase)) return iri[3..];
        if (iri.StartsWith("prop:", StringComparison.OrdinalIgnoreCase)) return iri[5..];
        int lastSlash = iri.LastIndexOf('/');
        int lastHash  = iri.LastIndexOf('#');
        int idx = Math.Max(lastSlash, lastHash);
        return idx >= 0 && idx < iri.Length - 1 ? iri[(idx + 1)..] : iri;
    }

    #endregion
}
