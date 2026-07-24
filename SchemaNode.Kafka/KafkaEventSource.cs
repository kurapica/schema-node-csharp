using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Event;
using SchemaNode.Context;
using System.Reflection;
using System.Text;
using System.Text.Json;
using SchemaNode.Service;

namespace SchemaNode.Kafka;

public sealed class KafkaEventSource : IEventSource
{
    private readonly IConsumer<string, byte[]> _consumer;
    private IReadOnlyDictionary<string, (Type, Type?)> _events = null!;
    private IServiceScopeFactory _factory = null!;

    public KafkaEventSource(IConsumer<string, byte[]> consumer)
    {
        _consumer = consumer;
    }

    public Task StartAsync(SchemaContext context, CancellationToken token)
    {
        _events = context.GetSchemaAssemblies() // Only scan registered schema assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && typeof(KafkaEvent).IsAssignableFrom(t) && t.IsDefined(typeof(KafkaTopicAttribute), false))
            .SelectMany(t => t.GetCustomAttributes<KafkaTopicAttribute>()
                .Select(attr => new { attr.Topic, Type = t }))
            .ToDictionary(x => x.Topic, x => (x.Type, x.Type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventPayload<>))?
                .GetGenericArguments()[0]));

        // Subscribe the topics
        _consumer.Subscribe(_events.Keys);

        // Get the scope factory
        _factory = context.GetRequiredService<IServiceScopeFactory>();

        Task.Run(() => ConsumeLoop(token), token);
        return Task.CompletedTask;
    }

    private void ConsumeLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var result = _consumer.Consume(token);
                if (!_events.TryGetValue(result.Topic, out var map))
                    continue;

                using var scope = _factory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<SchemaContext>();

                try
                {
                    var evt = (KafkaEvent)Activator.CreateInstance(map.Item1)!;
                    if (map.Item2 != null)
                    {
                        var payload = FromJson(Encoding.UTF8.GetString(result.Message.Value), map.Item2);
                        evt.Payload = context.GetSchemaNodeAsync(payload).GetAwaiter().GetResult();
                    }
                    context.RaiseEvent(evt);
                }
                catch(Exception ex)
                {
                    context.LogError(ex, $"Error consuming kafka [{result.Topic}]"); 
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error consuming Kafka message: {ex}");
            }
        }
    }

    public Task StopAsync(CancellationToken token)
    {
        _consumer.Close();
        return Task.CompletedTask;
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
