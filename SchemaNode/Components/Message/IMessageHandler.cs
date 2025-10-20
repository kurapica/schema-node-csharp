using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchemaNode.Context;

namespace SchemaNode.Components;

/// <summary>
/// The message handler
/// </summary>
public interface ISchemaMessageHandler<in T>
{
    Task HandleAsync(SchemaContext context, T message);
}

public interface ISchemaMessagePublisher
{
    Task PublishAsync<T>(T message);
}

static class SchemaMessageHandlerExtensions
{
    /// <summary>
    /// Register all message handlers in the assembly of the type
    /// </summary>
    public static void RegisterSchemaMessageHandlers<T>(IServiceCollection services)
    {
        RegisterSchemaMessageHandlers(services, typeof(T).Assembly);
    }
    
    /// <summary>
    /// Register all message handlers in the assembly
    /// </summary>
    public static void RegisterSchemaMessageHandlers(IServiceCollection services, Assembly? assembly)
    {
        if (assembly == null) return;
        foreach (Type type in assembly.GetTypes().Where(t => typeof(ISchemaMessageHandler<>).IsAssignableFrom(t)))
        {
            Type? handlerInterface = type.GetInterfaces().FirstOrDefault(t => typeof(ISchemaMessageHandler<>).IsAssignableFrom(t));
            if (handlerInterface == null) continue;
            services.TryAdd(new ServiceDescriptor(handlerInterface, type, ServiceLifetime.Scoped));
        }
    }

    /// <summary>
    /// Handle the message in schema context
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="message">The message</param>
    /// <typeparam name="T">The message type</typeparam>
    public static async Task<bool> HandleMessageAsync<T>(this SchemaContext context, T message)
    {
        var handler = context.ServiceProvider.GetService<ISchemaMessageHandler<T>>();
        if (handler == null) return false;
        await handler.HandleAsync(context, message).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Publish a schema message
    /// </summary>
    /// <param name="context">The context</param>
    /// <param name="message">The message</param>
    /// <typeparam name="T">The message type</typeparam>
    public static async Task<bool> PublishMessageAsync<T>(this SchemaContext context, T message)
    {
        var publisher = context.ServiceProvider.GetService<ISchemaMessagePublisher>();
        if (publisher == null) return false;
        await publisher.PublishAsync(message).ConfigureAwait(false);
        return true;
    }
}