namespace SchemaNode.AI.Ontology;

/// <summary>
/// Splits SSP-v1 text into (schemaKey, content) pairs.
/// Shared by index, init and event-source code paths.
/// </summary>
internal static class OntologySspParser
{
    /// <summary>
    /// Splits <paramref name="ssp"/> on <c>---</c> separators and returns every block
    /// that contains a <c>Schema: </c> header line as a (key, content) tuple.
    /// </summary>
    internal static IEnumerable<(string Key, string Content)> ParseBlocks(string ssp)
    {
        // Basic block split using SSP separators
        string[] segments = ssp.Split(new[] { "\n---\n", "\r\n---\r\n" }, StringSplitOptions.None);
        foreach (string segment in segments)
        {
            string trimmed = segment.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // Find primary schema key (original behavior)
            string? schemaKey = null;
            string[] lines = trimmed.Split('\n');
            foreach (string line in lines)
            {
                string l = line.Trim();
                if (l.StartsWith("Schema: ", StringComparison.Ordinal))
                {
                    schemaKey = l["Schema: ".Length..].Trim();
                    break;
                }
            }

            if (schemaKey != null)
            {
                // Yield the original block so existing consumers remain compatible
                yield return (schemaKey, trimmed);

                // Heuristic: try to emit more granular entries for enums, structs, functions and apps
                // to increase query hit-rate. This is intentionally resilient and permissive.

                // Normalize lines for case-insensitive header checks
                string lower = trimmed.ToLowerInvariant();

                // ENUM: look for type marker or presence of Values/Enum sections
                if (lower.Contains("type: enum") || lower.Contains("\nenum:") || lower.Contains("\nvalues:"))
                {
                    foreach (string val in ExtractEnumValues(lines))
                    {
                        if (string.IsNullOrWhiteSpace(val)) continue;
                        string key = $"enum:{schemaKey}:{val}";
                        string content = $"Enum {schemaKey} value {val}";
                        yield return (key, content);
                    }
                }

                // STRUCT: look for struct/fields markers
                if (lower.Contains("type: struct") || lower.Contains("\nstruct:") || lower.Contains("\nfields:"))
                {
                    foreach (string field in ExtractStructFields(lines))
                    {
                        if (string.IsNullOrWhiteSpace(field)) continue;
                        string key = $"struct:{schemaKey}:{field}";
                        string content = $"Struct {schemaKey} field {field}";
                        yield return (key, content);
                    }
                }

                // FUNCTION: look for function marker
                if (lower.Contains("type: function") || lower.Contains("\nfunction:") || lower.Contains("returns:") || lower.Contains("args:"))
                {
                    // record return type and each param
                    string funcName = schemaKey;
                    string? returnType = ExtractFunctionReturn(lines);
                    if (!string.IsNullOrWhiteSpace(returnType))
                    {
                        yield return ($"function:{funcName}:return:{returnType}", $"Function {funcName} return {returnType}");
                    }

                    foreach (var p in ExtractFunctionParams(lines))
                    {
                        yield return ($"function:{funcName}:param:{p.Name}", $"Function {funcName} param {p.Name} : {p.Type}");
                    }
                }

                // APP: record top-level fields/tables (workflows skipped)
                if (lower.Contains("type: app") || lower.Contains("\napp:") || schemaKey?.StartsWith("app.", StringComparison.OrdinalIgnoreCase) == true)
                {
                    foreach (string field in ExtractStructFields(lines))
                    {
                        if (string.IsNullOrWhiteSpace(field)) continue;
                        string key = $"app:{schemaKey}:field:{field}";
                        string content = $"App {schemaKey} field {field}";
                        yield return (key, content);
                    }
                }
            }
        }
    }

    // Helper: extract enum values heuristically from block lines
    static IEnumerable<string> ExtractEnumValues(string[] lines)
    {
        var vals = new List<string>();
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.StartsWith("- "))
            {
                string token = line[2..].Trim();
                // token might be "Value: pending" or just "pending"
                int idx = IndexOfIgnoreCase(token, "value:");
                if (idx >= 0)
                {
                    string v = token[(idx + "value:".Length)..].Trim();
                    vals.Add(CleanToken(v));
                }
                else
                {
                    // take first word or entire token
                    string v = token.Split(':', 2)[0].Trim();
                    vals.Add(CleanToken(v));
                }
            }
            else
            {
                int idx = IndexOfIgnoreCase(line, "value:");
                if (idx >= 0)
                {
                    string v = line[(idx + "value:".Length)..].Trim();
                    vals.Add(CleanToken(v));
                    continue;
                }

                // support lines like "  schemaCreate — system.schema..." (em dash separator)
                int em = line.IndexOf('—');
                if (em >= 0)
                {
                    string v = line[..em].Trim();
                    vals.Add(CleanToken(v));
                    continue;
                }

                // support simple "name: token" under indented lists
                int colon = line.IndexOf(':');
                if (colon > 0 && (raw.StartsWith(" ") || raw.StartsWith("\t")))
                {
                    string v = line[..colon].Trim();
                    if (!string.IsNullOrEmpty(v)) vals.Add(CleanToken(v));
                }
            }
        }
        return vals.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    // Helper: extract struct/app fields heuristically from block lines
    static IEnumerable<string> ExtractStructFields(string[] lines)
    {
        var fields = new List<string>();
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.StartsWith("- "))
            {
                string token = line[2..].Trim();
                int idx = IndexOfIgnoreCase(token, "name:");
                if (idx >= 0)
                {
                    string name = token[(idx + "name:".Length)..].Trim();
                    fields.Add(CleanToken(name));
                    continue;
                }

                // pattern like "- id: \n    Type: ..." -> token may be "id:"
                if (token.EndsWith(":"))
                {
                    string name = token[..^1].Trim();
                    fields.Add(CleanToken(name));
                    continue;
                }

                // fallback: token might simply be field name
                if (!token.Contains(" ") && token.Length > 0)
                {
                    fields.Add(CleanToken(token));
                    continue;
                }
            }
            else
            {
                int idx = IndexOfIgnoreCase(line, "name:");
                if (idx >= 0)
                {
                    string name = line[(idx + "name:".Length)..].Trim();
                    fields.Add(CleanToken(name));
                    continue;
                }

                // support lines like "  lang: String — system.localetran.lang"
                int colon = line.IndexOf(':');
                if (colon > 0 && (raw.StartsWith(" ") || raw.StartsWith("\t")))
                {
                    string name = line[..colon].Trim();
                    if (IsIdentifier(name)) fields.Add(CleanToken(name));
                }
            }
        }
        return fields.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    static bool IsIdentifier(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        foreach (char c in s)
        {
            if (!(char.IsLetterOrDigit(c) || c == '_' )) return false;
        }
        return true;
    }

    // Helper: extract function return type heuristically
    static string? ExtractFunctionReturn(string[] lines)
    {
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            int idx = IndexOfIgnoreCase(line, "return:");
            if (idx >= 0)
            {
                string v = line[(idx + "return:".Length)..].Trim();
                return CleanToken(v);
            }

            idx = IndexOfIgnoreCase(line, "returns:");
            if (idx >= 0)
            {
                string v = line[(idx + "returns:".Length)..].Trim();
                return CleanToken(v);
            }
        }
        return null;
    }

    // Helper: extract function params heuristically
    static (string Name, string Type)[] ExtractFunctionParams(string[] lines)
    {
        var list = new List<(string, string)>();
        string currentArgName = null!;
        string currentArgType = null!;
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.StartsWith("- "))
            {
                // new param entry
                string token = line[2..].Trim();
                int idx = IndexOfIgnoreCase(token, "name:");
                if (idx >= 0)
                {
                    currentArgName = CleanToken(token[(idx + "name:".Length)..].Trim());
                }
                else
                {
                    // token might be "argName: Type"
                    var parts = token.Split(':', 2);
                    if (parts.Length >= 1) currentArgName = CleanToken(parts[0]);
                    if (parts.Length == 2) currentArgType = CleanToken(parts[1]);
                }
                if (!string.IsNullOrWhiteSpace(currentArgName))
                {
                    list.Add((currentArgName, currentArgType ?? ""));
                    currentArgName = null!; currentArgType = null!;
                }
            }
            else
            {
                int idx = IndexOfIgnoreCase(line, "name:");
                if (idx >= 0)
                {
                    currentArgName = CleanToken(line[(idx + "name:".Length)..].Trim());
                }
                idx = IndexOfIgnoreCase(line, "type:");
                if (idx >= 0)
                {
                    currentArgType = CleanToken(line[(idx + "type:".Length)..].Trim());
                }
                if (!string.IsNullOrWhiteSpace(currentArgName))
                {
                    list.Add((currentArgName, currentArgType ?? ""));
                    currentArgName = null!; currentArgType = null!;
                }
            }
        }
        return list.ToArray();
    }

    static int IndexOfIgnoreCase(string s, string token) => s.IndexOf(token, StringComparison.OrdinalIgnoreCase);

    static string CleanToken(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        string r = s.Trim().Trim(',').Trim('"', '\'', '`');
        // remove trailing braces or commas
        r = r.TrimEnd(',', '}', ']');
        return r;
    }
}
