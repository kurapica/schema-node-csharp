namespace SchemaNode.RabbitMQ;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RabbitBindingAttribute(
    string exchange,
    string routingKey,
    string exchangeType = "topic")
    : System.Attribute
{
    /// <summary>
    /// The exchange name
    /// </summary>
    public string Exchange { get; } = exchange;

    /// <summary>
    /// The routing key
    /// </summary>
    public string RoutingKey { get; } = routingKey;

    /// <summary>
    /// The exchange type
    /// </summary>
    public string ExchangeType { get; } = exchangeType;
}