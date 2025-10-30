using SchemaNode.Schema;

namespace SchemaNode.Attribute;

/// <summary>
/// Declare system namespace
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Method)]
public class SchemaTypeAttribute: System.Attribute
{
    /// <summary>
    /// The namespace name
    /// </summary>
    public string? Name { get; }
    
    /// <summary>
    /// The display
    /// </summary>
    public LocaleString? Display { get; }

    /// <summary>
    /// The constructor
    /// </summary>
    /// <param name="name">The namespace</param>
    /// <param name="display">The display</param>
    public SchemaTypeAttribute(string? name = null, string? display = null)
    {
        Name = name;
        Display = display;
    }
    
    public SchemaTypeAttribute(string name, string key, string lang, string tran)
    {
        Name = name;
        Display = new LocaleString(key, (lang, tran));
    }
    
    public SchemaTypeAttribute(string name, string key, params string[] lang)
    {
        Name = name;
        List<(string lang, string tran)> translations = new();
        for (int i = 0; i < lang.Length - 2; i += 2)
        {
            translations.Add((lang[i], lang[i + 1]));
        }
        Display = new LocaleString(key, translations.ToArray());
    }
}