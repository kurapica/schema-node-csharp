using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Components;

namespace SchemaNode.Ontology;

/// <summary>
/// Dependency-injection extensions for <c>SchemaNode.Ontology</c>.
/// </summary>
public static class SchemaNodeOntologyInjection
{
    /// <summary>
    /// Registers the SchemaNode.Ontology assembly for API and format-provider discovery.
    /// <para>
    /// <b>Must be called before <c>AddSchemaNode</c></b> so that the ontology APIs and
    /// <see cref="OntologyFormatProvider"/> are included in the assembly scan.
    /// </para>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddSchemaOntology(this IServiceCollection services)
    {
        services.AddSchemaAssemblies(Assembly.GetExecutingAssembly());
        return services;
    }
}
