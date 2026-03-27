using System.Text.Json;

namespace SchemaNode.Schema;

/// <summary>
/// The interface for additional data
/// </summary>
public interface IAdditionalProperty
{
    /// <summary>
    /// The additional data
    /// </summary>
    Dictionary<string, JsonElement>? Additional { get; set; }
}

public static class AdditionalSchemaExtension
{
    /// <summary>
    /// Combine other additional data into this
    /// </summary>
    public static void CombineAdditionalProperty<T>(this T schema, T? other) where T : IAdditionalProperty
    {
        if (other == null) return;
        if (other.Additional is { Count: > 0 })
        {
            schema.Additional ??= [];
            foreach (var (key, value) in other.Additional)
                schema.Additional[key] = value;
        }
    }
}