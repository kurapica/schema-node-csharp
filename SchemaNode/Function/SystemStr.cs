using SchemaNode.Attribute;
using SchemaNode.Schema;

namespace SchemaNode.Function;

/// <summary>
/// System.Str apis
/// </summary>
[SchemaNameSpace("system.str")]
public static class SystemStr
{
    [SchemaFunc]
    public static string Concat(string str1, string str2) => string.Concat(str1, str2);
    
    [SchemaFunc]
    public static long Len(string str) => long.CreateChecked(str.Length);
    
    [SchemaFunc]
    public static string[] Split(string str, string sep) => str.Split(sep, StringSplitOptions.RemoveEmptyEntries);
    
    [SchemaFunc]
    public static string Substr(string str, int startIndex, int stop) => str.Substring(startIndex, stop - startIndex);

    [SchemaFunc]
    public static LocaleString ToLocale(string? str) => new LocaleString { Key = str ?? "" };
}