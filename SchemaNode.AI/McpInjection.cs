using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.AI.Mcp;
using SchemaNode.Schema;

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
        // Reuse the schema API serializer so node schema extension properties (display, struct,
        // enum, func, ...) are serialized as valid JSON in tool results.
        services.AddMcpServer()
            .WithToolsFromAssembly(typeof(SchemaTools).Assembly, SchemaNodeJsonExtension.GetSerializerOptions())
            .WithHttpTransport();
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
