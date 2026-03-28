using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.AI.Mcp;

namespace SchemaNode.AI;

/// <summary>
/// Dependency-injection extensions for the MCP (Model Context Protocol) feature of <c>SchemaNode.AI</c>.
/// </summary>
public static class SchemaNodeMcpInjection
{
    /// <summary>
    /// Registers the MCP server with schema tools and HTTP transport.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddSchemaMcp(this IServiceCollection services)
    {
        services.AddMcpServer().WithToolsFromAssembly(typeof(SchemaTools).Assembly).WithHttpTransport();
        return services;
    }

    /// <summary>
    /// Maps the MCP endpoint to the application pipeline.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <param name="prefix">The URL prefix for the MCP endpoint.</param>
    /// <returns>The same <see cref="WebApplication"/> for chaining.</returns>
    public static WebApplication MapSchemaMcp(this WebApplication app, string prefix = "/mcp")
    {
        app.MapMcp(prefix);
        return app;
    }
}
