using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Property.Function;
using SchemaNode.Enum;
using SchemaNode.Property.Common;
using SchemaNode.Struct;
using static SchemaNode.Utility.Constant;
using SchemaNode.Runtime;
using SchemaNode.Relation;
using SchemaNode.Schema;
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
        /// <summary>
        /// StartsWith
        /// </summary>
        [Meta<Property.Function.Logic>(LogicType.StartsWith)]
        public static bool startswith([Meta<Default>("")] string str, [Meta<Default>("")] string prefix) => !string.IsNullOrWhiteSpace(prefix) && str.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        
        /// <summary>
        /// NotStartsWith
        /// </summary>
        [Meta<Property.Function.Logic>(LogicType.NotStartsWith)]
        public static bool notstartswith([Meta<Default>("")] string str, [Meta<Default>("")] string prefix) => !string.IsNullOrWhiteSpace(prefix) && !str.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        
        /// <summary>
        /// EndsWith
        /// </summary>
        [Meta<Property.Function.Logic>(LogicType.EndsWith)]
        public static bool endswith([Meta<Default>("")] string str, [Meta<Default>("")] string suffix) => !string.IsNullOrWhiteSpace(suffix) && str.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        
        /// <summary>
        /// NotEndsWith
        /// </summary>
        [Meta<Property.Function.Logic>(LogicType.NotEndsWith)]
        public static bool notendswith([Meta<Default>("")] string str, [Meta<Default>("")] string suffix) => !string.IsNullOrWhiteSpace(suffix) && !str.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        
        /// <summary>
        /// Match
        /// </summary>
        [Meta<Property.Function.Logic>(LogicType.Match)]
        public static bool contains([Meta<Default>("")] string str, [Meta<Default>("")] string substr) => !string.IsNullOrWhiteSpace(substr) && str.Contains(substr, StringComparison.OrdinalIgnoreCase);
        
        /// <summary>
        /// NotMatch
        /// </summary>
        [Meta<Property.Function.Logic>(LogicType.NotMatch)]
        public static bool notcontains([Meta<Default>("")] string str, [Meta<Default>("")] string substr) => !string.IsNullOrWhiteSpace(substr) && !str.Contains(substr, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region State

    [Meta<SchemaType>($"{NS_SYSTEM_STR}.state")]
    public static class State
    {
        /// <summary>
        /// Length
        /// </summary>
        public static long length([Meta<Default>("")] string str = "") => long.CreateChecked(str.Length);
        
        /// <summary>
        /// IsEmpty
        /// </summary>
        public static bool isempty(string? str) => string.IsNullOrWhiteSpace(str);
    }

    #endregion

    #region Conversion

    [Meta<SchemaType>($"{NS_SYSTEM_STR}.convert")]
    public static class Convert
    {
        /// <summary>
        /// Concat
        /// </summary>
        public static string concat([Meta<Default>("")] string str1, [Meta<Default>("")] string str2) => string.Concat(str1, str2);
        
        /// <summary>
        /// Split
        /// </summary>
        public static string[] split([Meta<Default>("")] string str, [Meta<Default>("")] string sep) => str.Split(sep, StringSplitOptions.RemoveEmptyEntries);
        
        /// <summary>
        /// Substr
        /// </summary>
        public static string substr([Meta<Default>("")] string str, [Meta<Default>(0)] int startIndex, int? stop)
        {
            int start = Math.Clamp(startIndex, 0, str.Length);
            int end = Math.Clamp(stop ?? str.Length, start, str.Length);
            return str.Substring(start, end - start);
        }
        
        /// <summary>
        /// Replace
        /// </summary>
        public static string replace([Meta<Default>("")] string str, string search, string? replace = null) => str.Replace(search, replace ?? "");
        
        /// <summary>
        /// Trim
        /// </summary>
        public static string trim([Meta<Default>("")] string str) => str.Trim();
        
        /// <summary>
        /// ToLower
        /// </summary>
        public static string tolower([Meta<Default>("")] string str) => str.ToLower();
        
        /// <summary>
        /// ToUpper
        /// </summary>
        public static string toupper([Meta<Default>("")] string str) => str.ToUpper();
        
        /// <summary>
        /// Reverse
        /// </summary>
        public static string reverse([Meta<Default>("")] string str) => new string(str.Reverse().ToArray());
        
        /// <summary>
        /// PadLeft
        /// </summary>
        public static string padleft([Meta<Default>("")] string str, long totalWidth, char paddingChar = ' ') => str.PadLeft((int)totalWidth, paddingChar);
        
        /// <summary>
        /// PadRight
        /// </summary>
        public static string padright([Meta<Default>("")] string str, long totalWidth, char paddingChar = ' ') => str.PadRight((int)totalWidth, paddingChar);
        
        /// <summary>
        /// Repeat
        /// </summary>
        public static string repeat([Meta<Default>("")] string str, long count) => string.Concat(Enumerable.Repeat(str, (int)count));
    }

    #endregion

    #region Map

    [Meta<SchemaType>($"{NS_SYSTEM_STR}.map")]
    public static class Map
    {
        /// <summary>
        /// ToLocale
        /// </summary>
        [Meta<Converter>(true)]
        public static LocaleString tolocale(string? str) => new LocaleString(str ?? "");
        
        /// <summary>
        /// ToLocaleStr
        /// </summary>
        [Meta<Converter>(true)]
        public static string tolocalestr(LocaleString? locale) => locale?.Key ?? "";
        
        /// <summary>
        /// RectifyLocale
        /// </summary>
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

        /// <summary>
        /// ToEntryAccess
        /// </summary>
        [Relation<EntrySource, Assign>($"{nameof(key)}.{nameof(CallArg.Value)}", $"{NS_SYSTEM_SCHEMA_REFLECT_ARRAY}.{nameof(Reflect.Array.getelementaccessentries)}", $"{nameof(values)}.{nameof(CallArg.SourceType)}")]
        [Relation<EntrySource, Assign>($"{nameof(display)}.{nameof(CallArg.Value)}", $"{NS_SYSTEM_SCHEMA_REFLECT_ARRAY}.{nameof(Reflect.Array.getelementaccessentries)}", $"{nameof(values)}.{nameof(CallArg.SourceType)}")]
        public static EntryAccess<T>[] toentryaccess<T>(IEnumerable<IValueAccess> values,
            string key,
            string? display = null) where T: notnull
        {
            List<Entry<T>> children = [];
            foreach (var value in values)
            {
                var k = value.GetAccessValue(key);
                if (k != null && k.TryGetValue<T>(out T? v) && v != null)
                {
                    var e = new Entry<T> { Value = v };
                    var d = !string.IsNullOrWhiteSpace(display) ? value.GetAccessValue(display) : null;
                    if (d != null && d.TryGetValue(out LocaleString? s) && s != null)
                    {
                        e.SetProperty<Display, LocaleString>(s);
                    }
                    else
                    {
                        e.SetProperty<Display, LocaleString>(k.ToString());
                    }
                }
            }

            return [new EntryAccess<T>
            {
                Children = children.ToArray()
            }];
        }
    }

    #endregion

    #region Util

    [Meta<SchemaType>($"{NS_SYSTEM_STR}.util")]
    public static class Util
    {
        [Meta<ServerOnly>(true)]
        [Meta<NoCache>(true)]
        public static string newguid() => Guid.CreateVersion7().ToString();
    }


    #endregion
}