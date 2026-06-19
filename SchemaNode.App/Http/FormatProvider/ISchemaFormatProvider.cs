using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Http;
using SchemaNode.Runtime;
using System.Collections.Concurrent;
using System.Reflection;

namespace SchemaNode.Components;

/// <summary>
/// Provide the application schema format for download
/// </summary>
public interface ISchemaFormatProvider
{
    #region Abstract

    /// <summary>
    /// Generate the app schema output for the given format
    /// </summary>
    Task<SchemaApiFile?> GenerateAppSchemaOutput(SchemaContext context, AppType app, string format, CancellationToken cancellationToken);

    #endregion

    #region Static

    /// <summary>
    /// Gets the schema format provider for given format
    /// </summary>
    public static ISchemaFormatProvider? GetSchemaFormatProvider(string format)
    {
        format = format.ToLower();
        if (FormatProviders.TryGetValue(format, out Type? providerType))
        {
            return Activator.CreateInstance(providerType) as ISchemaFormatProvider;
        }
        return null;
    }

    /// <summary>
    /// Gets the supported formats
    /// </summary>
    public static IEnumerable<string> GetSupportedFormats() => FormatProviders.Keys;

    /// <summary>
    /// Add schema format provider
    /// </summary>
    internal static void AddSchemaFormatProvider(Type type)
    {
        if (!type.IsAssignableTo(typeof(ISchemaFormatProvider))) return;
        foreach (var attr in type.GetCustomAttributes<SchemaFormatAttribute>())
        {
            if (string.IsNullOrWhiteSpace(attr.Format)) continue;
            FormatProviders.TryAdd(attr.Format.ToLower(), type);
        }
    }

    static readonly ConcurrentDictionary<string, Type> FormatProviders = [];

    #endregion
}
