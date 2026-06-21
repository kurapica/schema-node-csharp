using System.Text.Json;

namespace SchemaNode.Schema;

/// <summary>
/// The interface for extensions
/// </summary>
public interface ISchemaExtensions
{
    /// <summary>
    /// The extensions
    /// </summary>
    Dictionary<string, JsonElement>? Extensions { get; set; }
}

public static class SchemaExtensionsHelper
{
    /// <summary>
    /// Combine other extensions into this
    /// </summary>
    public static void CombineExtensions<T>(this T schema, T? other) where T : ISchemaExtensions
    {
        if (other == null) return;
        if (other.Extensions is { Count: > 0 })
        {
            schema.Extensions ??= [];
            foreach (var (key, value) in other.Extensions)
                schema.Extensions[key] = value;
        }
    }
}