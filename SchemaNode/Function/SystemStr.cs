using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Schema;

namespace SchemaNode.Function;

/// <summary>
/// System.Str apis
/// </summary>
[Schema("system.str")]
public static class SystemStr
{
    [Schema]
    public static string Concat(string str1, string str2) => string.Concat(str1, str2);
    
    [Schema]
    public static long Len(string str) => long.CreateChecked(str.Length);
    
    [Schema]
    public static string[] Split(string str, string sep) => str.Split(sep, StringSplitOptions.RemoveEmptyEntries);
    
    [Schema]
    public static string Substr(string str, int startIndex, int? stop) => str.Substring(startIndex, (stop ?? str.Length) - startIndex);

    [Schema]
    public static LocaleString ToLocale(string? str) => new LocaleString (str ?? "");

    [Schema]
    public static Entry ToEntry(StructTypeNode node, string valueField, string labelField)
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
    public static List<Entry> ToEntrys(ArrayTypeNode array, string valueField, string labelField) => array
        .OfType<StructTypeNode>()
        .Select(node => ToEntry(node, valueField, labelField))
        .ToList();

    [Schema]
    public static string NewGuid() => Guid.NewGuid().ToString();
}