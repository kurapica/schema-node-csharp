namespace SchemaNode.Attribute;

[AttributeUsage(AttributeTargets.Class)]
public class PluginAttribute(string identity) : System.Attribute
{
    /// <summary>
    /// The unique plugin identity for discovery
    /// </summary>
    public string Identity { get; } = identity;
}