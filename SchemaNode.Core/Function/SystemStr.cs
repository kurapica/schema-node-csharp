using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Property.Schema;
using SchemaNode.Property.Function;
using SchemaNode.Enum;
using SchemaNode.Struct;
using static SchemaNode.Utility.Constant;
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace SchemaNode.Function;

/// <summary>
/// System.Str apis
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_STR)]
public static class SystemStr
{
    #region Logic

    [Meta<SchemaType>($"{NS_SYSTEM_STR}.logic")]
    public static class Logic
    {
        [Meta<Property.Function.Logic>(LogicType.StartsWith)]
        public static bool startswith([Meta<Default>("")] string str, [Meta<Default>("")] string prefix) => !string.IsNullOrWhiteSpace(prefix) && str.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        
        [Meta<Property.Function.Logic>(LogicType.NotStartsWith)]
        public static bool notstartswith([Meta<Default>("")] string str, [Meta<Default>("")] string prefix) => !string.IsNullOrWhiteSpace(prefix) && !str.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        
        [Meta<Property.Function.Logic>(LogicType.EndsWith)]
        public static bool endswith([Meta<Default>("")] string str, [Meta<Default>("")] string suffix) => !string.IsNullOrWhiteSpace(suffix) && str.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        
        [Meta<Property.Function.Logic>(LogicType.NotEndsWith)]
        public static bool notendswith([Meta<Default>("")] string str, [Meta<Default>("")] string suffix) => !string.IsNullOrWhiteSpace(suffix) && !str.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        
        [Meta<Property.Function.Logic>(LogicType.Match)]
        public static bool contains([Meta<Default>("")] string str, [Meta<Default>("")] string substr) => !string.IsNullOrWhiteSpace(substr) && str.Contains(substr, StringComparison.OrdinalIgnoreCase);
        
        [Meta<Property.Function.Logic>(LogicType.NotMatch)]
        public static bool notcontains([Meta<Default>("")] string str, [Meta<Default>("")] string substr) => !string.IsNullOrWhiteSpace(substr) && !str.Contains(substr, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region State

    [Meta<SchemaType>($"{NS_SYSTEM_STR}.state")]
    public static class State
    {
        public static long len([Meta<Default>("")] string str) => long.CreateChecked(str.Length);
        
        public static bool isempty(string? str) => string.IsNullOrWhiteSpace(str);
    }

    #endregion

    #region Conversion

    [Meta<SchemaType>($"{NS_SYSTEM_STR}.convert")]
    public static class Convert
    {
        public static string concat([Meta<Default>("")] string str1, [Meta<Default>("")] string str2) => string.Concat(str1, str2);
        
        public static string[] split([Meta<Default>("")] string str, [Meta<Default>("")] string sep) => str.Split(sep, StringSplitOptions.RemoveEmptyEntries);
        
        public static string substr([Meta<Default>("")] string str, [Meta<Default>(0)] int startIndex, int? stop)
        {
            int start = Math.Clamp(startIndex, 0, str.Length);
            int end = Math.Clamp(stop ?? str.Length, start, str.Length);
            return str.Substring(start, end - start);
        }
        
        public static string replace([Meta<Default>("")] string str, string search, string? replace = null) => str.Replace(search, replace ?? "");
        
        public static string trim([Meta<Default>("")] string str) => str.Trim();
        
        public static string tolower([Meta<Default>("")] string str) => str.ToLower();
        
        public static string toupper([Meta<Default>("")] string str) => str.ToUpper();
        
        public static string reverse([Meta<Default>("")] string str) => new string(str.Reverse().ToArray());
        
        public static string padleft([Meta<Default>("")] string str, long totalWidth, char paddingChar = ' ') => str.PadLeft((int)totalWidth, paddingChar);
        
        public static string padright([Meta<Default>("")] string str, long totalWidth, char paddingChar = ' ') => str.PadRight((int)totalWidth, paddingChar);
        
        public static string repeat([Meta<Default>("")] string str, long count) => string.Concat(Enumerable.Repeat(str, (int)count));
    }

    #endregion

    #region Map

    [Meta<SchemaType>($"{NS_SYSTEM_STR}.map")]
    public static class Map
    {
        [Meta<Converter>]
        public static LocaleString tolocale(string? str) => new LocaleString(str ?? "");
        
        [Meta<Converter>]
        public static string tolocalestr(LocaleString? locale) => locale?.Key ?? "";
        
        public static Entry toentry(StructNode node, string valueField, string labelField)
        {
            Node.DataNode? val = node.GetValueByPaths(valueField);
            Node.DataNode? label = node.GetValueByPaths(labelField);
            return new Entry
            {
                Value = val?.ToTypeValue(typeof(string))?.ToString() ?? "",
                Label = label switch
                {
                    StructNode labelNode => labelNode.ToTypeValue(typeof(LocaleString)) as LocaleString,
                    ScalarNode or EnumNode => new LocaleString(label.ToTypeValue(typeof(string))?.ToString() ?? ""),
                    _ => new LocaleString(val?.ToTypeValue(typeof(string))?.ToString() ?? "")
                }
            };
        }
        
        public static List<Entry> toentrys(ArrayNode array, string valueField, string labelField) => array
            .OfType<StructNode>()
            .Select(node => toentry(node, valueField, labelField))
            .DistinctBy(p => p.Value)
            .ToList();
        
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
    }

    #endregion

    #region Util

    [Meta<SchemaType>($"{NS_SYSTEM_STR}.util")]
    public static class Util
    {
        [Meta<ServerOnly>]
        [Meta<NoCache>]
        public static string newguid() => Guid.CreateVersion7().ToString();
    }


    #endregion
}