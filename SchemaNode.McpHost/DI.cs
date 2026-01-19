using Microsoft.Extensions.DependencyInjection;
using SchemaNode.McpHost.Tools;

namespace SchemaNode.McpHost;

public static class DI
{
    public static IServiceCollection AddMcpHost(this IServiceCollection services)
    {
        // Add McpHost related services here if needed in the future
        services.AddMcpServer().WithToolsFromAssembly(typeof(SchemaTools).Assembly).WithHttpTransport();
        return services;
    }
}