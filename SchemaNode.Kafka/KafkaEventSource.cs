using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Utility;
using System.Reflection;
using System.Text;

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
        _events = Injection.GetRegisteredAssemblies() // Only scan registered schema assemblies
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
                    context.RaiseEvent(evt, map.Item2 != null ? Encoding.UTF8.GetString(result.Message.Value).FromJson(map.Item2) : null);
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
}
