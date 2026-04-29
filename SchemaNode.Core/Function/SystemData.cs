using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using System.Text.Json.Nodes;
using static SchemaNode.Utility.Constant;
using SchemaNode.Property.Function;
using SchemaNode.Property.Schema;
using SchemaType = SchemaNode.Property.Schema.SchemaType;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace SchemaNode.Function;

/// <summary>
/// The system.data api
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_DATA)]
public static class SystemData
{
    [Meta<SchemaType>($"{NS_SYSTEM_DATA}.enum")]
    public static class EnumOper
    {
        /// <summary>
        /// Check the value is descendant of the root value
        /// </summary>
        public static async Task<bool> isdescendant(SchemaContext context, [Meta<SchemaType>(NS_SYSTEM_SCHEMA_TYPE_ENUM)] string @enum, string value, string root)
        {
            value = value.Trim();
            root = root.Trim();
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(root)) return false;
            if (value.Equals(root)) return true;

            // Check with value access
            EnumType? enumType = await context.GetNodeTypeAsync<EnumType>(@enum);
            if (enumType == null) return false;
            EnumValueAccess[] access = await enumType.LoadEnumAccessListAsync(context, value, noSubList: true);
            return access.Any(a => a.Value.Equals(root));
        }

        /// <summary>
        /// Check the value is descendant of any root value
        /// </summary>
        public static async Task<bool> isdescendantany(SchemaContext context, [Meta<SchemaType>(NS_SYSTEM_SCHEMA_TYPE_ENUM)] string @enum, string value, string[] roots)
        {
            value = value.Trim();
            var rootSet = new HashSet<string>(roots.Select(r => r.Trim()));
            if (string.IsNullOrWhiteSpace(value) || roots.Length == 0) return false;
            if (roots.Any(r => r.Equals(value))) return true;

            // Check with value access
            EnumType? enumType = await context.GetNodeTypeAsync<EnumType>(@enum);
            if (enumType == null) return false;
            EnumValueAccess[] access = await enumType.LoadEnumAccessListAsync(context, value, noSubList: true);
            return access.Any(a => rootSet.Contains(a.Value));
        }

        /// <summary>
        /// Gets the enum value's root with the given depth, if -1 means the last root, the root is 0, if the depth is bigger than the actual depth, return empty string
        /// </summary>
        public static async Task<string> parent(SchemaContext context, [Meta<SchemaType>(NS_SYSTEM_SCHEMA_TYPE_ENUM)] string @enum, string value, int depth = 0)
        {
            value = value.Trim();
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            // Check with value access
            EnumType? enumType = await context.GetNodeTypeAsync<EnumType>(@enum);
            if (enumType == null) return string.Empty;
            EnumValueAccess[] access = await enumType.LoadEnumAccessListAsync(context, value, noSubList: true);
            return depth < 0 
                ? access.Length > 1-depth ? access[access.Length + depth - 1].Value : string.Empty
                : access.Length > depth ? access[depth].Value : string.Empty;
        }

        /// <summary>
        /// Gets the enum value's depth, the root is 0
        /// </summary>
        public static async Task<long> depth(SchemaContext context, [Meta<SchemaType>(NS_SYSTEM_SCHEMA_TYPE_ENUM)] string @enum, string value)
        {
            value = value.Trim();
            if (string.IsNullOrWhiteSpace(value)) return -1;
            // Check with value access
            EnumType? enumType = await context.GetNodeTypeAsync<EnumType>(@enum);
            if (enumType == null) return -1;
            EnumValueAccess[] access = await enumType.LoadEnumAccessListAsync(context, value, noSubList: true);
            return access.Length - 1;
        }

        /// <summary>
        /// The lowest common ancestor
        /// </summary>
        public static async Task<string> lca(SchemaContext context, [Meta<SchemaType>(NS_SYSTEM_SCHEMA_TYPE_ENUM)] string @enum, string[] values)  
        {
            values = values.Select(v => v.Trim()).Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();
            if (values.Length == 0) return string.Empty;
            EnumType? enumType = await context.GetNodeTypeAsync<EnumType>(@enum);
            if (enumType == null) return string.Empty;
            EnumValueAccess[] access = await enumType.LoadEnumAccessListAsync(context, values[0], noSubList: true);
            for (int i = 1; i < values.Length; i++)
            {
                EnumValueAccess[] next = await enumType.LoadEnumAccessListAsync(context, values[i], noSubList: true);
                if (next.Length == 0) { access = []; break; }
                for (int j = 0; j < access.Length && j < next.Length; j++)
                {
                    if (!access[j].Value.Equals(next[j].Value))
                    {
                        access = access.Take(j).ToArray();
                        break;
                    }
                }
                if (access.Length > next.Length) access = access.Take(next.Length).ToArray();
                if (access.Length == 0) break;
            }
            return access.Length > 0 ? access[access.Length - 1].Value : string.Empty;
        }
    }
}