using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Service;
using System.Reflection;

namespace SchemaNode.AI;

/// <summary>
/// Dependency-injection extensions for the ontology feature of <c>SchemaNode.AI</c>.
/// </summary>
public static class SchemaNodeOntologyInjection
{
    /// <summary>
    /// Registers the <c>SchemaNode.AI</c> assembly for API and format-provider discovery,
    /// enabling the ontology APIs and <see cref="OntologyFormatProvider"/>.
    /// <para>
    /// <b>Must be called before <c>AddSchemaNode</c></b> so that the ontology APIs and
    /// <see cref="OntologyFormatProvider"/> are included in the assembly scan.
    /// </para>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional delegate to configure <see cref="OntologyOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddSchemaOntology(
        this IServiceCollection services,
        Action<OntologyOptions>? configure = null)
    {
        var options = new OntologyOptions();
        configure?.Invoke(options);
        OntologyOptions.Apply(options);

        return services.AddAppSchemaAssemblies(Assembly.GetExecutingAssembly());
    }
}
