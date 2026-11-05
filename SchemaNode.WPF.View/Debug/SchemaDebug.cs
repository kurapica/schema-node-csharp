using System.Globalization;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Runtime;

namespace SchemaNode.WPF.View.Debug;

/// <summary>
/// Debug-only switch for the per-node inspector. Release builds never create the inspector; the
/// host turns it on from a DEBUG-compiled block so production surfaces stay untouched.
/// </summary>
public static class SchemaDebug
{
    /// <summary>Whether rendered nodes show the inspector button.</summary>
    public static bool Enabled { get; set; }

    /// <summary>Builds the snapshot shown by the inspector for <paramref name="node"/>.</summary>
    public static DebugNodeSnapshot Describe(DataNode node)
    {
        List<DebugPropertyRow> properties = [];
        foreach (IProperty property in node.GetProperties<IProperty>())
        {
            // Schema-definition bookkeeping is not user-visible value state; the Vue debug view
            // hides the same set.
            if (!property.HasValue || !property.Savable) continue;

            string source = property.Source is { } access
                ? $"{access.Access} ({access.Type?.Name})"
                : string.Empty;
            properties.Add(new DebugPropertyRow(property.Name, Format(property.GetValue<object>()), source));
        }

        List<DebugRelationRow> relations = [];
        foreach (IRelation relation in node.GetAttachedRelations())
        {
            relations.Add(new DebugRelationRow(
                relation.Target,
                relation.PropertyCtor?.Name ?? "?",
                relation.HasDepends ? "depends" : "direct",
                relation.Owner?.Name ?? string.Empty));
        }

        return new DebugNodeSnapshot
        {
            Access = node.Access,
            TypeName = node.Type?.Name ?? "?",
            Id = node.Id.ToString(),
            Properties = properties,
            Relations = relations
        };
    }

    private static string Format(object? value)
    {
        if (value is null) return "null";
        if (value is string text) return text;
        if (value is System.Collections.IEnumerable sequence and not string)
            return $"[{string.Join(", ", sequence.Cast<object?>().Select(Format))}]";
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }
}

/// <summary>One row of the node property table.</summary>
public sealed class DebugPropertyRow(string name, string value, string source)
{

    /// <summary>Property name.</summary>
    public string Name { get; } = name;

    /// <summary>Current value.</summary>
    public string Value { get; } = value;

    /// <summary>Access that supplied the value (node access path and type).</summary>
    public string Source { get; } = source;
}

/// <summary>One row of the node relation table.</summary>
public sealed class DebugRelationRow(string property, string mode, string data, string source)
{

    /// <summary>Target property the relation writes.</summary>
    public string Property { get; } = property;

    /// <summary>Relation process type.</summary>
    public string Mode { get; } = mode;

    /// <summary>Dependency summary.</summary>
    public string Data { get; } = data;

    /// <summary>Owner access path.</summary>
    public string Source { get; } = source;
}

/// <summary>Snapshot displayed by the node inspector.</summary>
public sealed class DebugNodeSnapshot
{
    /// <summary>Access path of the node.</summary>
    public string Access { get; init; } = string.Empty;

    /// <summary>Schema type name.</summary>
    public string TypeName { get; init; } = string.Empty;

    /// <summary>Node id.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Property rows.</summary>
    public IReadOnlyList<DebugPropertyRow> Properties { get; init; } = [];

    /// <summary>Relation rows.</summary>
    public IReadOnlyList<DebugRelationRow> Relations { get; init; } = [];

    /// <summary>Header line: access, type and id, mirroring the Vue debug view.</summary>
    public string Header => $"{Access}  ({TypeName})  #{Id}";
}
