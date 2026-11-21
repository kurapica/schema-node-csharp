using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SchemaNode.Schema;

namespace SchemaNode.AI.Chat;

/// <summary>
/// JSON helpers shared by the chat service and the streaming endpoint.
/// </summary>
internal static class SchemaChatJson
{
    /// <summary>
    /// Options used for prompt rendering, SSE payloads and tool results. The options come from the
    /// schema API so node schema extension properties are serialized correctly.
    /// </summary>
    public static readonly JsonSerializerOptions Options = SchemaNodeJsonExtension.GetSerializerOptions();

    private static readonly string[] NodeKeyOrder =
    [
        "namespace", "name", "kind", "display", "description",
        "struct", "enum", "array", "func", "generics", "relations",
    ];

    private static readonly string[] FieldKeyOrder =
    [
        "name", "type", "display", "description", "require", "default", "valid",
        "upLimit", "lowLimit", "minSize", "maxSize", "readOnly",
    ];

    private static readonly string[] EntryKeyOrder = ["value", "display", "disabled", "children"];

    /// <summary>
    /// Rebuilds a schema JSON object with a stable key order. The model is sensitive to property
    /// order; normalizing it makes the same editor content produce the same prompt regardless of
    /// which client or JSON serializer sent it.
    /// </summary>
    public static JsonNode? NormalizeForPrompt(JsonNode? node)
    {
        if (node is not JsonObject root) return node?.DeepClone();

        JsonObject result = Reorder(root, NodeKeyOrder);

        if (result["struct"] is JsonObject structDef && structDef["fields"] is JsonArray fields)
        {
            JsonObject orderedStruct = Reorder(structDef, ["fields"]);
            JsonArray orderedFields = [];
            foreach (JsonNode? field in fields)
                orderedFields.Add(field is JsonObject fieldObject ? Reorder(fieldObject, FieldKeyOrder) : field?.DeepClone());
            orderedStruct["fields"] = orderedFields;
            result["struct"] = orderedStruct;
        }

        if (result["enum"] is JsonObject enumDef && enumDef["values"] is JsonArray values)
        {
            JsonObject orderedEnum = Reorder(enumDef, ["type", "cascade", "values"]);
            JsonArray orderedValues = [];
            foreach (JsonNode? entry in values)
                orderedValues.Add(entry is JsonObject entryObject ? Reorder(entryObject, EntryKeyOrder) : entry?.DeepClone());
            orderedEnum["values"] = orderedValues;
            result["enum"] = orderedEnum;
        }

        return result;
    }

    /// <summary>
    /// Deterministic fallback for "English default + zhCN translation" requests: moves the current
    /// Chinese label into trans and derives an English key from the schema/field/value name.
    /// </summary>
    public static JsonNode? LocalizeDisplays(JsonNode? node)
    {
        if (node is not JsonObject) return null;

        JsonNode clone = node.DeepClone();
        Walk(clone);
        return clone;

        static void Walk(JsonNode? current)
        {
            if (current is JsonObject obj)
            {
                if (obj["display"] is JsonObject display
                    && GetString(display, "key") is { Length: > 0 } key
                    && ContainsCjk(key)
                    && !HasChineseTranslation(display))
                {
                    string? name = GetString(obj, "name") ?? GetString(obj, "value");
                    display["trans"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["lang"] = "zhCN",
                            ["tran"] = key,
                        },
                    };
                    display["key"] = EnglishLabel(name);
                }

                foreach (KeyValuePair<string, JsonNode?> property in obj.ToList())
                    Walk(property.Value);
            }
            else if (current is JsonArray array)
            {
                foreach (JsonNode? item in array)
                    Walk(item);
            }
        }
    }

    private static bool ContainsCjk(string text)
        => text.Any(ch => ch is >= '\u4e00' and <= '\u9fff');

    private static bool HasChineseTranslation(JsonObject display)
        => display["trans"] is JsonArray translations
           && translations.Any(item => item is JsonObject entry
                                       && string.Equals(GetString(entry, "lang"), "zhCN", StringComparison.OrdinalIgnoreCase));

    private static readonly Dictionary<string, string> CommonLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["min"] = "Minimum",
        ["max"] = "Maximum",
        ["id"] = "ID",
        ["url"] = "URL",
        ["uuid"] = "UUID",
        ["name"] = "Name",
        ["email"] = "Email",
        ["age"] = "Age",
        ["value"] = "Value",
        ["count"] = "Count",
        ["price"] = "Price",
        ["amount"] = "Amount",
        ["date"] = "Date",
        ["time"] = "Time",
        ["status"] = "Status",
        ["type"] = "Type",
        ["description"] = "Description",
    };

    private static string EnglishLabel(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Label";
        if (CommonLabels.TryGetValue(name, out string? known)) return known;

        string spaced = Regex.Replace(name, "([a-z0-9])([A-Z])", "$1 $2").Replace('_', ' ').Replace('-', ' ');
        string[] words = spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length == 0
            ? "Label"
            : string.Join(' ', words.Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    }

    private static JsonObject Reorder(JsonObject source, string[] priority)
    {
        JsonObject result = [];

        foreach (string key in priority)
        {
            if (source.TryGetPropertyValue(key, out JsonNode? value) && value != null)
                result[key] = value.DeepClone();
        }

        foreach (string key in source.Select(p => p.Key)
                     .Where(k => !result.ContainsKey(k))
                     .OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            result[key] = source[key]?.DeepClone();
        }

        return result;
    }

    /// <summary>
    /// Produces a compact, backend computed list of changed JSON paths between the editor content
    /// and the candidate. This is independent from the model's own summary.
    /// </summary>
    public static string[] Diff(JsonNode? before, JsonNode? after)
    {
        if (before == null && after == null) return [];
        if (before == null) return ["+ (new schema)"];

        List<string> changes = [];
        DiffNode(string.Empty, before, after, changes, 0);
        return changes.Take(40).ToArray();
    }

    private static void DiffNode(string path, JsonNode? before, JsonNode? after, List<string> changes, int depth)
    {
        if (changes.Count >= 40 || depth > 8) return;

        string label = string.IsNullOrEmpty(path) ? "(root)" : path;

        if (before is JsonObject beforeObject && after is JsonObject afterObject)
        {
            HashSet<string> keys = new(beforeObject.Select(p => p.Key), StringComparer.OrdinalIgnoreCase);
            keys.UnionWith(afterObject.Select(p => p.Key));

            foreach (string key in keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                string childPath = string.IsNullOrEmpty(path) ? key : $"{path}.{key}";
                bool inBefore = beforeObject.TryGetPropertyValue(key, out var beforeValue);
                bool inAfter = afterObject.TryGetPropertyValue(key, out var afterValue);

                if (inBefore && !inAfter)
                    changes.Add($"-{childPath}");
                else if (!inBefore && inAfter)
                    changes.Add($"+{childPath}");
                else
                    DiffNode(childPath, beforeValue, afterValue, changes, depth + 1);
            }
            return;
        }

        if (before is JsonArray beforeArray && after is JsonArray afterArray)
        {
            if (TryKeyedDiff(path, beforeArray, afterArray, changes)) return;
            if (!JsonNode.DeepEquals(beforeArray, afterArray))
                changes.Add($"~{label}");
            return;
        }

        if (!JsonNode.DeepEquals(before, after))
        {
            string beforeText = Truncate(before?.ToJsonString() ?? "null");
            string afterText = Truncate(after?.ToJsonString() ?? "null");
            changes.Add($"~{label}: {beforeText} -> {afterText}");
        }
    }

    private static bool TryKeyedDiff(string path, JsonArray before, JsonArray after, List<string> changes)
    {
        Dictionary<string, JsonNode?> beforeMap = MapByName(before);
        Dictionary<string, JsonNode?> afterMap = MapByName(after);
        if (beforeMap.Count == 0 && afterMap.Count == 0) return false;

        HashSet<string> keys = new(beforeMap.Keys, StringComparer.OrdinalIgnoreCase);
        keys.UnionWith(afterMap.Keys);

        foreach (string key in keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            string childPath = string.IsNullOrEmpty(path) ? $"[{key}]" : $"{path}[{key}]";
            bool inBefore = beforeMap.TryGetValue(key, out var beforeValue);
            bool inAfter = afterMap.TryGetValue(key, out var afterValue);

            if (inBefore && !inAfter)
                changes.Add($"-{childPath}");
            else if (!inBefore && inAfter)
                changes.Add($"+{childPath}");
            else
                DiffNode(childPath, beforeValue, afterValue, changes, 0);
        }
        return true;
    }

    private static Dictionary<string, JsonNode?> MapByName(JsonArray array)
    {
        Dictionary<string, JsonNode?> map = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonNode? item in array)
        {
            if (item is not JsonObject obj) continue;
            string? key = GetString(obj, "name") ?? GetString(obj, "value");
            if (!string.IsNullOrWhiteSpace(key)) map[key] = obj;
        }
        return map;
    }

    private static string? GetString(JsonObject obj, string key)
    {
        if (!obj.TryGetPropertyValue(key, out var node) || node is not JsonValue value) return null;
        return value.TryGetValue<string>(out var text) ? text : node.ToString();
    }

    private static string Truncate(string text)
        => text.Length <= 80 ? text : $"{text[..77]}...";
}
