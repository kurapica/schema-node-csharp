using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Schema;

namespace SchemaNode.Function;

/// <summary>
/// System.Str apis
/// </summary>
[SchemaType("system.str")]
public static class SystemStr
{
    [SchemaType]
    public static string Concat(string str1, string str2) => string.Concat(str1, str2);
    
    [SchemaType]
    public static long Len(string str) => long.CreateChecked(str.Length);
    
    [SchemaType]
    public static string[] Split(string str, string sep) => str.Split(sep, StringSplitOptions.RemoveEmptyEntries);
    
    [SchemaType]
    public static string Substr(string str, int startIndex, int? stop) => str.Substring(startIndex, (stop ?? str.Length) - startIndex);

    [SchemaType]
    public static LocaleString ToLocale(string? str) => new LocaleString (str ?? "");

    [SchemaType]
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

    [SchemaType]
    public static List<Entry> ToEntrys(ArrayTypeNode array, string valueField, string labelField) => array
        .OfType<StructTypeNode>()
        .Select(node => ToEntry(node, valueField, labelField))
        .ToList();

    [SchemaType]
    public static string NewGuid() => Guid.NewGuid().ToString();
}