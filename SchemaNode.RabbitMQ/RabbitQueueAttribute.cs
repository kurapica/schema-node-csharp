namespace SchemaNode.RabbitMQ;

/// <summary>
/// The RabbitMQ queue attribute
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RabbitQueueAttribute(string queue) : System.Attribute
{
    /// <summary>
    /// The queue name
    /// </summary>
    public string Queue { get; } = queue;
}