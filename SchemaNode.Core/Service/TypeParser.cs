using Microsoft.Extensions.DependencyInjection;

namespace SchemaNode.Service;

/// <summary>
/// The interface for type parser, used to convert the type information to others, like schema and etc
/// </summary>
public interface ITypeParser;

public static class TypeParserExtensions
{
    /// <summary>
    /// Add a type parser to the service collection, the type must implement ITypeParser interface
    /// </summary>
    public static IServiceCollection AddTypeParser<T>(this IServiceCollection services) where T : class, ITypeParser
    {
        return services.AddSingleton<ITypeParser, T>();
    }
}