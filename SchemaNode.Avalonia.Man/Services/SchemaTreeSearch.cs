using SchemaNode.Schema;

namespace SchemaNode.Avalonia.Man.Services;

/// <summary>
/// Helpers for walking the namespace tree returned by the schema API.
/// </summary>
public static class SchemaTreeSearch
{
    /// <summary>
    /// Finds a definition by full name anywhere in the API result tree.
    /// A namespace entry carries its own definition and nests its direct children.
    /// </summary>
    public static NodeSchema? Find(NodeSchema[]? schemas, string fullName)
    {
        if (schemas is null) return null;
        foreach (NodeSchema schema in schemas)
        {
            if (schema.FullName.Equals(fullName, StringComparison.OrdinalIgnoreCase)) return schema;
            if (Find(schema.Schemas, fullName) is { } found) return found;
        }
        return null;
    }
}
