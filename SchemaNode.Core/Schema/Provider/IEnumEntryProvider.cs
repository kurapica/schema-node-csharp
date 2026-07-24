using SchemaNode.Struct;

namespace SchemaNode.Schema.Provider;

/// <summary>
/// The enum schema provider
/// </summary>
public interface IEnumEntryProvider
{
    /// <summary>
    /// Gets the enum entry access list by value
    /// </summary>
    /// <param name="schemaName">The enum type</param>
    /// <param name="value">The given value, if null get the children of the start value</param>
    /// <param name="start">The access start value</param>
    /// <returns></returns>
    Task<EntryAccess<string>[]> GetEnumEntryAccessAsync(string schemaName, string? value, string? start = null);
}