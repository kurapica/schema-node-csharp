namespace SchemaNode.Http;

/// <summary>
/// Mark an schema api as enabele feature
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class FeatureAttribute(string feature) : System.Attribute
{
    /// <summary>
    /// The enabled feature name
    /// </summary>
    public string Feature { get; } = feature;
}
