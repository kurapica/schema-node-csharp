namespace SchemaNode.Attribute;

/// <summary>
/// The app schema format
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class SchemaFormatAttribute: System.Attribute
{
    /// <summary>
    /// The support format
    /// </summary>
    public string Format { get; } = null!;

    public SchemaFormatAttribute(string format)
    {
        Format = format.Trim().ToLower();
    }
}
