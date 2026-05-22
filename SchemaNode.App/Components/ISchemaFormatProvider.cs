using System.Collections.Concurrent;
using System.Reflection;
using SchemaNode.App.Attribute;
using SchemaNode.Context;

namespace SchemaNode.App.Components;

/// <summary>
/// Provides a downloadable schema output for a specific export format.
/// </summary>
public interface ISchemaFormatProvider
{
    /// <summary>Generates the export output for the given application type.</summary>
    Task<SchemaNode.Http.SchemaApiFile?> GenerateAppSchemaOutput(SchemaContext context, AppType app, string format, CancellationToken cancellationToken);

    #region Registry

    /// <summary>Returns the <see cref="ISchemaFormatProvider"/> registered for <paramref name="format"/>, or <c>null</c>.</summary>
    public static ISchemaFormatProvider? GetSchemaFormatProvider(string format)
    {
        format = format.ToLower();
        return _providers.TryGetValue(format, out Type? t) ? Activator.CreateInstance(t) as ISchemaFormatProvider : null;
    }

    /// <summary>Returns all registered format keys.</summary>
    public static IEnumerable<string> GetSupportedFormats() => _providers.Keys;

    /// <summary>Registers all format providers found in the given assembly.</summary>
    public static void RegisterFromAssembly(Assembly assembly)
    {
        foreach (Type type in assembly.GetTypes())
            if (type.IsClass && !type.IsAbstract && type.IsAssignableTo(typeof(ISchemaFormatProvider)))
                AddSchemaFormatProvider(type);
    }

    internal static void AddSchemaFormatProvider(Type type)
    {
        if (!type.IsAssignableTo(typeof(ISchemaFormatProvider))) return;
        foreach (var attr in type.GetCustomAttributes<SchemaFormatAttribute>())
        {
            if (!string.IsNullOrWhiteSpace(attr.Format))
                _providers.TryAdd(attr.Format.ToLower(), type);
        }
    }

    static readonly ConcurrentDictionary<string, Type> _providers = [];

    #endregion
}
