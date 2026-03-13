using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Utility;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace SchemaNode.RabbitMQ;

public sealed class RabbitEventSource : IEventSource
{
    private readonly IConnection _connection;
    private readonly IReadOnlyDictionary<string, (Type, Type?, IEnumerable<RabbitBindingAttribute>)> _events;
    private IServiceScopeFactory _factory = null!;
    private IChannel _channel = null!;

    public RabbitEventSource(IConnection connection)
    {
        _connection = connection;

        // Scan event mappings at construction or StartAsync (both OK)
        _events = Injection.GetRegisteredAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract
                && typeof(RabbitEvent).IsAssignableFrom(t)
                && t.IsDefined(typeof(RabbitQueueAttribute), false))
            .SelectMany(t => t.GetCustomAttributes<RabbitQueueAttribute>()
                .Select(attr => new
                {
                    attr.Queue,
                    Type = t,
                    PayloadType = t.GetInterfaces()
                        .FirstOrDefault(i => i.IsGenericType
                            && i.GetGenericTypeDefinition() == typeof(IEventPayload<>))?
                        .GetGenericArguments()[0],
                    Binding = t.GetCustomAttributes<RabbitBindingAttribute>()
                }))
            .ToDictionary(x => x.Queue, x => (x.Type, x.PayloadType, x.Binding));
    }

    public async Task StartAsync(SchemaContext context, CancellationToken token)
    {
        _factory = context.GetRequiredService<IServiceScopeFactory>();
        _channel = await _connection.CreateChannelAsync(cancellationToken: token);
        
        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 10,
            global: false,
            cancellationToken: token);
        
        foreach (var queue in _events.Keys)
        {
            await _channel.QueueDeclareAsync(
                queue: queue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null, cancellationToken: token);

            var binding = _events[queue].Item3;
            foreach (var bind in binding)
            {
                await _channel.ExchangeDeclareAsync(
                    exchange: bind.Exchange,
                    type: bind.ExchangeType,
                    durable: true,
                    autoDelete: false,
                    arguments: null, cancellationToken: token);

                await _channel.QueueBindAsync(
                    queue: queue,
                    exchange: bind.Exchange,
                    routingKey: bind.RoutingKey,
                    arguments: null, cancellationToken: token);
            }

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                HandleMessage(queue, ea.Body.ToArray());
                await _channel.BasicAckAsync(ea.DeliveryTag, false, token);
            };

            await _channel.BasicConsumeAsync(
                queue: queue,
                autoAck: false,
                consumer: consumer, cancellationToken: token);
        }
    }

    private void HandleMessage(string queue, byte[] body)
    {
        if (!_events.TryGetValue(queue, out var map))
            return;

        using var scope = _factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SchemaContext>();

        try
        {
            var evt = (RabbitEvent)Activator.CreateInstance(map.Item1)!;

            object? payload = null;
            if (map.Item2 != null)
            {
                payload = FromJson(Encoding.UTF8.GetString(body), map.Item2);
            }

            context.RaiseEvent(evt, payload);
        }
        catch (Exception ex)
        {
            context.LogError(ex, $"Error consuming RabbitMQ [{queue}]");
        }
    }

    public async Task StopAsync(CancellationToken token)
    {
        try
        {
            await _channel.CloseAsync(cancellationToken: token);
            await _connection.CloseAsync(cancellationToken: token);
        }
        catch
        {
            // Ignore
        }
    }

    /// <summary>
    /// Deserializes a JSON string to a .NET value.
    /// </summary>
    internal static object? FromJson(string value, Type type)
    {
        if (type == typeof(string))
            return value;
        if (type == typeof(DateTimeOffset))
            return DateTimeOffset.Parse(value);
        if (type == typeof(DateTime))
            return DateTime.Parse(value);

        return JsonSerializer.Deserialize(value, type);
    }
}
