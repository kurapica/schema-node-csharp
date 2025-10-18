namespace SchemaNode.Attribute;

/// <summary>
/// The index attribute from structs
/// index without name will be treated as primary key
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class IndexAttribute: System.Attribute
{
    /// <summary>
    /// The index name
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// The order
    /// </summary>
    public int Order { get; } = 0;

    public IndexAttribute(int order = 0)
    {
        Order = order;
    }

    public IndexAttribute(string name, int order = 0)
    {
        Name = name;
        Order = order;
    }
}
