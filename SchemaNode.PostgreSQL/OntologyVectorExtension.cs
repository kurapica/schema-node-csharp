using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SchemaNode.Vector.Services;

namespace SchemaNode.PostgreSQL;

/// <summary>
/// Dependency-injection extensions for ontology vector storage in PostgreSQL.
/// </summary>
public static class OntologyVectorExtension
{
    /// <summary>
    /// Registers <see cref="OntologyVectorPostgreSqlService"/> as the
    /// <see cref="IOntologyVectorService"/> implementation.
    /// </summary>
    public static IServiceCollection AddOntologyVectorPostgreSql(
        this IServiceCollection services)
    {
        return services.AddSingleton<IOntologyVectorService, OntologyVectorPostgreSqlService>();
    }

    /// <summary>
    /// Registers an <see cref="NpgsqlDataSource"/> with pgvector support enabled
    /// and the <see cref="OntologyVectorPostgreSqlService"/> in a single call.
    /// <para>
    /// Use this overload instead of a plain <c>AddNpgsqlDataSource</c> call when you
    /// intend to use the ontology vector features — pgvector requires
    /// <c>UseVector()</c> on the data-source builder.
    /// </para>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="configure">Optional callback for additional builder configuration.</param>
    public static IServiceCollection AddNpgsqlDataSourceWithVector(
        this IServiceCollection services,
        string connectionString,
        Action<NpgsqlDataSourceBuilder>? configure = null)
    {
        services.AddNpgsqlDataSource(connectionString, builder =>
        {
            builder.UseVector();
            configure?.Invoke(builder);
        });
        return services.AddOntologyVectorPostgreSql();
    }
}
