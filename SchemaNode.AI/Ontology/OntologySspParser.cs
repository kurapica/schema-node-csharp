namespace SchemaNode.AI;

/// <summary>
/// Parses SSP-v1 text into <see cref="SemanticAtom"/> instances and generates granular
/// atoms directly from an <see cref="OntologyGraph"/> model.
/// <list type="bullet">
///   <item>
///     <see cref="ParseBlocks"/> — splits SSP text on <c>---</c> separators and emits one
///     <see cref="SemanticKind.Block"/> atom per schema block (backward-compatible).
///   </item>
///   <item>
///     <see cref="ParseAtoms"/> — iterates the typed <see cref="OntologyGraph"/> model and
///     emits fine-grained atoms (<see cref="SemanticKind.VocabularyValue"/>,
///     <see cref="SemanticKind.StructField"/>, <see cref="SemanticKind.FunctionParam"/>,
///     <see cref="SemanticKind.AppTable"/>).  Labels are resolved from
///     <see cref="OntologyLabel"/> arrays that were already populated during graph
///     construction (including database-stored locale strings).
///   </item>
/// </list>
/// </summary>
public static class OntologySspParser
{
    // ── Block atoms (SSP text → one atom per schema block) ────────────────────

    /// <summary>
    /// Splits <paramref name="ssp"/> on <c>---</c> separators and emits one
    /// <see cref="SemanticKind.Block"/> atom per block that contains a <c>Schema:</c> header.
    /// </summary>
    public static IEnumerable<SemanticAtom> ParseBlocks(string ssp)
    {
        string[] segments = ssp.Split(["\n---\n", "\r\n---\r\n"], StringSplitOptions.None);
        foreach (string segment in segments)
        {
            string trimmed = segment.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            string? schemaKey = null;
            foreach (string line in trimmed.Split('\n'))
            {
                string l = line.Trim();
                if (l.StartsWith("Schema: ", StringComparison.Ordinal))
                {
                    schemaKey = l["Schema: ".Length..].Trim();
                    break;
                }
            }

            if (schemaKey != null)
                yield return new SemanticAtom
                {
                    Id      = schemaKey,
                    Kind    = SemanticKind.Block,
                    Name    = schemaKey,
                    Parent  = null,
                    Content = trimmed,
                };
        }
    }

    // ── Granular atoms (OntologyGraph model → typed fine-grained atoms) ───────

    /// <summary>
    /// Iterates the <see cref="OntologyGraph"/> model and emits fine-grained atoms for the
    /// given <paramref name="locale"/>.  Labels are taken from the pre-resolved
    /// <see cref="OntologyLabel"/> arrays on each model object (populated from both the
    /// JSON locale files and the database during graph construction).
    /// </summary>
    /// <param name="graph">The typed ontology model.</param>
    /// <param name="locale">
    /// Language tag to select (e.g. <c>"enUS"</c>, <c>"zhCN"</c>).
    /// Pass <see langword="null"/> for the default label.
    /// </param>
    public static IEnumerable<SemanticAtom> ParseAtoms(OntologyGraph graph, string? locale = null)
    {
        // §1 Vocabulary / Enum — one atom per value
        foreach (OntologyEnumClass ec in graph.EnumClasses)
        {
            string parentLabel = ResolveLabel(ec.Labels, locale, ec.Name);
            foreach (OntologyEnumValue val in ec.Values)
            {
                string label = ResolveLabel(val.Labels, locale, val.Value);
                yield return new SemanticAtom
                {
                    Id      = $"{ec.Name}.{val.Value}",
                    Kind    = SemanticKind.VocabularyValue,
                    Name    = val.Value,
                    Parent  = ec.Name,
                    Content = FormatVocabularyValue(locale, val.Value, parentLabel, label),
                };
            }
        }

        // §2 Entity / Struct — one atom per property
        foreach (OntologyEntityClass ec in graph.EntityClasses)
        {
            string parentLabel = ResolveLabel(ec.Labels, locale, ec.Name);
            foreach (OntologyEntityProperty prop in ec.Properties)
            {
                string label = ResolveLabel(prop.Labels, locale, prop.Name);
                yield return new SemanticAtom
                {
                    Id      = $"{ec.Name}.{prop.Name}",
                    Kind    = SemanticKind.StructField,
                    Name    = prop.Name,
                    Parent  = ec.Name,
                    Content = FormatStructField(locale, prop.Name, parentLabel, label, prop.RangeIri),
                };
            }
        }

        // §3 Function — one atom per argument
        foreach (OntologyFunctionClass fc in graph.FunctionClasses)
        {
            foreach (OntologyFunctionArg arg in fc.Args)
            {
                string label = ResolveLabel(arg.Labels, locale, arg.Name);
                yield return new SemanticAtom
                {
                    Id      = $"{fc.Name}.param.{arg.Name}",
                    Kind    = SemanticKind.FunctionParam,
                    Name    = arg.Name,
                    Parent  = fc.Name,
                    Content = FormatFunctionParam(locale, arg.Name, fc.Name, label, arg.TypeStr),
                };
            }
        }

        // §4 App container — one atom per table field
        foreach (OntologyAppClass ac in graph.AppClasses)
        {
            string parentLabel = ResolveLabel(ac.Labels, locale, ac.Name);
            foreach (OntologyTableField tbl in ac.Tables)
            {
                string label = ResolveLabel(tbl.Labels, locale, tbl.Name);
                yield return new SemanticAtom
                {
                    Id      = $"{ac.Name}.{tbl.Name}",
                    Kind    = SemanticKind.AppTable,
                    Name    = tbl.Name,
                    Parent  = ac.Name,
                    Content = FormatAppTable(locale, tbl.Name, parentLabel, label, tbl.RangeIri),
                };
            }
        }
    }

    // ── Label resolution ──────────────────────────────────────────────────────

    /// <summary>
    /// Picks the best label text from <paramref name="labels"/> for <paramref name="locale"/>.
    /// Falls back to the default label (Language == null) or to <paramref name="fallback"/>.
    /// </summary>
    static string ResolveLabel(OntologyLabel[] labels, string? locale, string fallback)
    {
        if (labels.Length == 0) return fallback;

        if (!string.IsNullOrEmpty(locale))
        {
            string normalized = locale.Replace("-", "");
            // Exact contains (e.g. "zhCN")
            foreach (OntologyLabel lbl in labels)
            {
                if (lbl.Language != null
                 && lbl.Language.Replace("-", "").Equals(normalized, StringComparison.OrdinalIgnoreCase))
                    return lbl.Value;
            }
            // Language-prefix contains (e.g. "zh" matches "zhCN")
            if (normalized.Length >= 2)
            {
                string prefix = normalized[..2];
                foreach (OntologyLabel lbl in labels)
                {
                    if (lbl.Language != null
                     && lbl.Language.Replace("-", "").StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        return lbl.Value;
                }
            }
        }

        // Default label (Language == null)
        foreach (OntologyLabel lbl in labels)
            if (lbl.Language == null) return lbl.Value;

        return labels[0].Value;
    }

    // ── Content templates ─────────────────────────────────────────────────────

    static string FormatVocabularyValue(string? locale, string name, string parentLabel, string label)
    {
        bool zh = IsZh(locale);
        return zh
            ? $"词汇表 {parentLabel} 中的枚举值 {name}，含义：{label}"
            : $"Vocabulary value {name} of {parentLabel}, meaning: {label}";
    }

    static string FormatStructField(string? locale, string name, string parentLabel, string label, string typeIri)
    {
        bool zh = IsZh(locale);
        return zh
            ? $"实体 {parentLabel} 中的字段 {name}，类型：{typeIri}，含义：{label}"
            : $"Field {name} of type {typeIri} in entity {parentLabel}, meaning: {label}";
    }

    static string FormatFunctionParam(string? locale, string name, string funcName, string label, string typeStr)
    {
        bool zh = IsZh(locale);
        return zh
            ? $"函数 {funcName} 的参数 {name}，类型：{typeStr}，含义：{label}"
            : $"Parameter {name} of type {typeStr} in function {funcName}, meaning: {label}";
    }

    static string FormatAppTable(string? locale, string name, string parentLabel, string label, string typeIri)
    {
        bool zh = IsZh(locale);
        return zh
            ? $"应用 {parentLabel} 的数据表 {name}，类型：{typeIri}，含义：{label}"
            : $"Table {name} of type {typeIri} in app {parentLabel}, meaning: {label}";
    }

    static bool IsZh(string? locale) =>
        locale != null && locale.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
}
