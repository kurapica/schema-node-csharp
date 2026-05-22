namespace SchemaNode.App.Attribute;

/// <summary>
/// Marks an <see cref="SchemaNode.App.Components.ISchemaFormatProvider"/> implementation as supporting a specific export format.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class SchemaFormatAttribute : System.Attribute
{
    /// <summary>The export format key, e.g. "json", "xml".</summary>
    public string Format { get; }

    public SchemaFormatAttribute(string format)
    {
        Format = format.Trim().ToLower();
    }
}
