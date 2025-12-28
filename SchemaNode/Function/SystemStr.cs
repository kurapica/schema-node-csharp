using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
// ReSharper disable InconsistentNaming

namespace SchemaNode.Function;

/// <summary>
/// System.Str apis
/// </summary>
[Schema("system.str")]
public static class SystemStr
{
    #region Logic

    [Schema]
    [LogicExp(LogicExpType.StartsWith)]
    public static bool startswith([Default("")] string str, string prefix) => str.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    [Schema]
    [LogicExp(LogicExpType.EndsWith)]
    public static bool endswith([Default("")] string str, string suffix) => str.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);

    [Schema]
    [LogicExp(LogicExpType.Match)]
    public static bool match([Default("")] string str, string substr) => str.IndexOf(substr, StringComparison.OrdinalIgnoreCase) >= 0;

    #endregion

    [Schema]
    public static long len([Default("")] string str) => long.CreateChecked(str.Length);

    #region Conversion

    [Schema]
    public static string concat([Default("")] string str1, [Default("")] string str2) => string.Concat(str1, str2);
    
    [Schema]
    public static string[] split([Default("")] string str, [Default("")] string sep) => str.Split(sep, StringSplitOptions.RemoveEmptyEntries);
    
    [Schema]
    public static string substr([Default("")] string str, [Default(0)] int startIndex, int? stop) => str.Substring(startIndex, (stop ?? str.Length) - startIndex);

    [Schema]
    public static string replace([Default("")] string str, string search, string? replace = null) => str.Replace(search, replace ?? "");

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

    #endregion
}