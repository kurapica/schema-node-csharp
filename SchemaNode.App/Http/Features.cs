namespace SchemaNode.Http;

/// <summary>
/// Reads the enabled feature set inside the current process.
/// <para>
/// Both transports answer from the same registry: HTTP hosts publish it through
/// <c>ProtocolApi.EnableFeatures</c>, while in-process hosts such as
/// the WPF shell ask this service directly instead of fetching the protocol response.
/// </para>
/// </summary>
public interface ISchemaFeatures
{
    /// <summary>Whether the named feature is enabled. Comparison is case insensitive.</summary>
    /// <param name="feature">The feature name, e.g. <c>ai_chat</c>.</param>
    bool IsEnabled(string feature);

    /// <summary>The enabled feature names.</summary>
    string[] GetEnabledFeatures();
}

/// <summary>
/// Default <see cref="ISchemaFeatures"/> implementation backed by the shared
/// <see cref="Service.GetEnableFeatures"/> registry.
/// </summary>
public sealed class SchemaFeatures : ISchemaFeatures
{
    /// <inheritdoc />
    public bool IsEnabled(string feature)
    {
        if (string.IsNullOrWhiteSpace(feature)) return false;
        return GetEnabledFeatures().Contains(feature.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public string[] GetEnabledFeatures() => Service.GetEnableFeatures() ?? [];
}
