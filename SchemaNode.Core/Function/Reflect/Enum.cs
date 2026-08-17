using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Struct;
using EnumType = SchemaNode.Runtime.EnumType;
using static SchemaNode.Utility.Constant;
using ArrayType = SchemaNode.Schema.ArrayType;
using ValueType = SchemaNode.Runtime.ValueType;

// ReSharper disable InconsistentNaming

namespace SchemaNode.Function.Reflect;


/// <summary>
/// The reflection helpers for the schema enums
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_SCHEMA_REFLECT_ENUM)]
public static class Enum
{
    /// <summary>
    /// Gets the entry type for the given enum value type
    /// </summary>
    public static string getvaluetype(EnumValueType type)
    {
        switch (type)
        {
            case EnumValueType.Int:
            case EnumValueType.Flags:
                return $"{NS_SYSTEM_ENTRYS}<${NS_SYSTEM_INT}";
            default:
                return $"{NS_SYSTEM_ENTRYS}<${NS_SYSTEM_STRING}";
        }
    }

    /// <summary>
    /// Checks if the enum type has the given value type
    /// </summary>
    public static async Task<bool> isenumvaluetype(SchemaContext context, [Meta<SchemaType>(typeof(Schema.EnumType))] string type, EnumValueType valuetype)
    {
        var enumType = await context.getEnumType(type);
        return enumType?.Type == valuetype;
    }

    /// <summary>
    /// Gets the default entry value for the given enum value type
    /// </summary>
    public static string getdefaultentryvalue(EnumValueType type, Entry<string>[] values)
    {
        if (type != EnumValueType.Flags) return "";
        if (values == null || values.Length == 0) return "0";
        if (int.TryParse(values.Last().Value, out var lastValue))
        {
            int value = 1;
            while (value <= lastValue)
                value <<= 1;
            return value.ToString();
        }
        return "";
    }

    /// <summary>
    /// Checks if the type has cascades
    /// </summary>
    public static async Task<bool> hascascade(SchemaContext context, [Meta<SchemaType>(typeof(AnyType))] string type, bool onlyEnum = false)
    {
        var nodeType = await context.GetNodeTypeAsync(type);
        if (nodeType is Runtime.ArrayType a) nodeType = a.Element;
        if (onlyEnum) return nodeType is EnumType { Cascade.Length: > 0 };
        return nodeType is EnumType { Cascade.Length: > 0 } || nodeType?.GetProperty<EntrySource>() is { HasValue: true };
    }

    /// <summary>
    /// Gets the cascades for the given enum type
    /// </summary>
    public static async Task<Entry<int>[]> getcascades(SchemaContext context, [Meta<SchemaType>(typeof(Schema.EnumType))] string type)
    {
        var enumType = await context.getEnumType(type);
        return enumType?.Cascade?.Select((c, i) =>
        {
            var entry = new Entry<int>
            {
                Value = i + 1,
                HasChildren = false
            };
            entry.SetProperty<Display, LocaleString>(c);
            return entry;
        })?.ToArray() ?? [];
    }

    /// <summary>
    /// Gets the enum entry access list
    /// </summary>
    public static async Task<EntryAccess<string>[]> getenumaccess(SchemaContext context, [Meta<SchemaType>(typeof(Schema.EnumType))] string @enum, string? value, string? root)
    {
        // Check with value access
        EnumType? enumType = await context.getEnumType(@enum);
        if (enumType == null) return [];
        return await enumType.GetEnumEntryAccessAsync(context, value, root);
    }

    /// <summary>
    /// Check the value is descendant of the root value
    /// </summary>
    public static async Task<bool> isdescendant(SchemaContext context, [Meta<SchemaType>(typeof(Schema.EnumType))] string @enum, string value, string root)
    {
        value = value.Trim();
        root = root.Trim();
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(root)) return false;
        if (value.Equals(root)) return true;

        // Check with value access
        EnumType? enumType = await context.getEnumType(@enum);
        if (enumType == null) return false;
        var access = await enumType.GetEnumEntryAccessAsync(context, value, root);
        return access is { Length: > 0 };
    }

    /// <summary>
    /// Check the value is descendant of any root value
    /// </summary>
    public static async Task<bool> isdescendantany(SchemaContext context, [Meta<SchemaType>(typeof(Schema.EnumType))] string @enum, string value, string[] roots)
    {
        value = value.Trim();
        var rootSet = new HashSet<string>(roots.Select(r => r.Trim()));
        if (string.IsNullOrWhiteSpace(value) || roots.Length == 0) return false;
        if (roots.Any(r => r.Equals(value))) return true;

        // Check with value access
        EnumType? enumType = await context.getEnumType(@enum);
        if (enumType == null) return false;
        var access = await enumType.GetEnumEntryAccessAsync(context, value);
        return access.Any(a => a.Entry?.Value is not null && rootSet.Contains(a.Entry.Value));
    }

    /// <summary>
    /// Gets the enum value's root with the given depth, if -1 means the last root, the root is 0, if the depth is bigger than the actual depth, return empty string
    /// </summary>
    public static async Task<string?> parent(SchemaContext context, [Meta<SchemaType>(typeof(Schema.EnumType))] string @enum, string value, int depth = 0)
    {
        value = value.Trim();
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        // Check with value access
        EnumType? enumType = await context.getEnumType(@enum);
        if (enumType == null) return string.Empty;
        var access = await enumType.GetEnumEntryAccessAsync(context, value);
        return depth < 0 
            ? access.Length > 1-depth ? access[access.Length + depth - 1].Entry?.Value : null
            : access.Length > depth ? access[depth].Entry?.Value : null;
    }

    /// <summary>
    /// Gets the enum value's depth, the root is 0
    /// </summary>
    public static async Task<long> depth(SchemaContext context, [Meta<SchemaType>(typeof(Schema.EnumType))] string @enum, string value)
    {
        value = value.Trim();
        if (string.IsNullOrWhiteSpace(value)) return -1;
        // Check with value access
        EnumType? enumType = await context.getEnumType(@enum);
        if (enumType == null) return -1;
        var access = await enumType.GetEnumEntryAccessAsync(context, value);
        return access.Length - 1;
    }

    /// <summary>
    /// The lowest common ancestor
    /// </summary>
    public static async Task<string?> lca(SchemaContext context, [Meta<SchemaType>(typeof(Schema.EnumType))] string @enum, string[] values)  
    {
        values = values.Select(v => v.Trim()).Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();
        if (values.Length == 0) return string.Empty;
        EnumType? enumType = await context.getEnumType(@enum);
        if (enumType == null) return string.Empty;
        var access = await enumType.GetEnumEntryAccessAsync(context, values[0]);
        for (int i = 1; i < values.Length; i++)
        {
            var next = await enumType.GetEnumEntryAccessAsync(context, values[i]);
            if (next.Length == 0) { access = []; break; }
            for (int j = 1; j < access.Length && j < next.Length; j++)
            {
                if (!access[j].Entry!.Value.Equals(next[j].Entry?.Value))
                {
                    access = access.Take(j).ToArray();
                    break;
                }
            }
            if (access.Length > next.Length) access = access.Take(next.Length).ToArray();
            if (access.Length <= 1) break;
        }
        return access.Length > 1 ? access[^1].Entry?.Value : null;
    }

    static async Task<EnumType?> getEnumType(this SchemaContext context, string @enum)
    {
        var type = await context.GetNodeTypeAsync<ValueType>(@enum);
        if (type is Runtime.ArrayType a) type = a.Element;
        return type as EnumType;
    }
}