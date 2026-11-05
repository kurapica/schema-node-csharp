using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.AI.Agents;
using SchemaNode.Service;

namespace SchemaNode.AI.Workflow;

/// <summary>
/// Dependency-injection registration for the AI workflow node.
/// </summary>
public static class SchemaNodeAIWorkflowInjection
{
    /// <summary>
    /// Registers the AI workflow node assembly (so the workflow engine discovers
    /// <c>system.workflow.ai.generate</c>) and the agent provider options/factory.
    /// Environment variables are applied first; the delegate can override them, typically by
    /// binding <see cref="AIAgentOptions"/> from configuration.
    /// </summary>
    public static IServiceCollection AddSchemaAiWorkflow(
        this IServiceCollection services,
        Action<AIAgentOptions>? configure = null)
    {
        AIAgentOptions options = AIAgentOptions.FromEnvironment();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<AIAgentProviderFactory>(_ => new AIAgentProviderFactory(options));

        return services.AddAppSchemaAssemblies(Assembly.GetExecutingAssembly());
    }
}
