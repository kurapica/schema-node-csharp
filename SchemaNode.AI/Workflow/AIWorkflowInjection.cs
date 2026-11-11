using System.Reflection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchemaNode.AI.Chat;
using SchemaNode.Service;

namespace SchemaNode.AI.Workflow;

/// <summary>
/// Dependency-injection registration for the AI workflow node.
/// </summary>
public static class SchemaNodeAIWorkflowInjection
{
    /// <summary>
    /// Registers the AI workflow node assembly (so the workflow engine discovers
    /// <c>system.workflow.ai.generate</c>) and the shared chat client. <see cref="SchemaNodeChatInjection.AddSchemaChat"/>
    /// calls this automatically; a host that only runs workflows can call it directly.
    /// </summary>
    public static IServiceCollection AddSchemaAiWorkflow(
        this IServiceCollection services,
        Action<SchemaChatOptions>? configure = null)
    {
        var options = new SchemaChatOptions();
        configure?.Invoke(options);
        services.TryAddSingleton(options);

        services.TryAddSingleton<IChatClient>(serviceProvider =>
            SchemaNodeChatInjection.CreateClient(serviceProvider.GetRequiredService<SchemaChatOptions>()));

        return services.AddAppSchemaAssemblies(Assembly.GetExecutingAssembly());
    }
}
