using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.McpHost.Tools;

namespace SchemaNode.McpHost;

public static class Injection
{
    public static IServiceCollection AddSchemaMcpHost(this IServiceCollection services)
    {
        // Add McpHost related services here if needed in the future
        services.AddMcpServer().WithToolsFromAssembly(typeof(SchemaTools).Assembly).WithHttpTransport();
        return services;
    }

    public static WebApplication MapSchemaMcpHost(this WebApplication app, string prefix = "/mcp")
    {
        app.MapMcp(prefix);
        return app;
    }
}