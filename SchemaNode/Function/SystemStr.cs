using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Schema;
// ReSharper disable InconsistentNaming

namespace SchemaNode.Function;

/// <summary>
/// System.Str apis
/// </summary>
[Schema("system.str")]
public static class SystemStr
{
    [Schema]
    public static string concat(string str1, string str2) => string.Concat(str1, str2);
    
    [Schema]
    public static long len(string str) => long.CreateChecked(str.Length);
    
    [Schema]
    public static string[] split(string str, string sep) => str.Split(sep, StringSplitOptions.RemoveEmptyEntries);
    
    [Schema]
    public static string substr(string str, int startIndex, int? stop) => str.Substring(startIndex, (stop ?? str.Length) - startIndex);

    [Schema]
    public static bool startswith(string str, string prefix) => str.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    
    [Schema]
    public static bool endswith(string str, string suffix) => str.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
    
    [Schema]
    public static bool contains(string str, string substr) => str.IndexOf(substr, StringComparison.OrdinalIgnoreCase) >= 0;
    
    [Schema]
    public static LocaleString tolocale(string? str) => new LocaleString (str ?? "");

    [Schema]
    public static Entry toentry(StructTypeNode node, string valueField, string labelField)
    {
        AnySchemaNode? val = node.GetValueByPaths(valueField);
        AnySchemaNode? label = node.GetValueByPaths(labelField);
        return new Entry
        {
            Value = val?.ToTypeValue(typeof(string))?.ToString() ?? "",
            Label = label is StructTypeNode labelNode 
                ? labelNode.ToTypeValue(typeof(LocaleString)) as LocaleString 
                : label is ScalarTypeNode or EnumTypeNode                
                    ? new LocaleString (label.ToTypeValue(typeof(string))?.ToString() ?? "" ) 
                    : new LocaleString ( val?.ToTypeValue(typeof(string))?.ToString() ?? "" )
        };
    }

    [Schema]
    public static List<Entry> toentrys(ArrayTypeNode array, string valueField, string labelField) => array
        .OfType<StructTypeNode>()
        .Select(node => toentry(node, valueField, labelField))
        .DistinctBy(p => p.Value)
        .ToList();
    
    [Schema]
    public static LocaleString rectifylocale(LocaleString locale, string? defaultLang = null)
    {
        if (string.IsNullOrWhiteSpace(locale.Key))
        {
            locale.Key = (string.IsNullOrWhiteSpace(defaultLang)
                ? locale.Trans?.FirstOrDefault()?.Tran
                : locale.Trans?.FirstOrDefault(t => t.Lang.Equals(defaultLang, StringComparison.OrdinalIgnoreCase))?.Tran ?? locale.Key) ?? "";
        }
        return locale;
    }

    [Schema]
    public static string newguid() => Guid.NewGuid().ToString();
    
    [Schema]
    public static string replace(string str, string search, string? replace = null) => str.Replace(search, replace ?? "");
}