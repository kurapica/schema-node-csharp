using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Struct;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using ArrayType = SchemaNode.Runtime.ArrayType;
using EnumType = SchemaNode.Runtime.EnumType;
using PropertyType = SchemaNode.Runtime.PropertyType;
using ValueSchemaKind = SchemaNode.Property.Record.ValueSchemaKind;
using ValueType = SchemaNode.Schema.ValueType;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace SchemaNode.Function;

/// <summary>
/// The reflection helpers for the system schema, used for validating the schema nodes and functions in the system schema
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_SCHEMA_REFLECT)]
public static class SystemReflect
{
    /// <summary>
    /// Gets the full names and labels of the schema nodes under the namespace with the given name
    /// </summary>
    public static async Task<List<EntryAccess<string>>> gettypeentries(SchemaContext context,
        [Meta<SchemaType>(typeof(AnyType))] string? name = null, string? root = null)
    {
        if (!string.IsNullOrWhiteSpace(root) && !string.IsNullOrWhiteSpace(name) && !name.Equals(root, StringComparison.OrdinalIgnoreCase) && !name.StartsWith($"{root}.", StringComparison.OrdinalIgnoreCase))
            return []; // not access-able
        
        var ns = await context.GetNodeTypeAsync<Runtime.NodeType>(string.IsNullOrWhiteSpace(name) ? (root ?? "") : name);
        if (ns == null) return [];

        List<EntryAccess<string>> result = [];
        while (ns != null)
        {
            var access = new EntryAccess<string>();
            if (ns.Namespace != null)
            {
                access.Entry = new Entry<string>()
                {
                    Value = ns.Name,
                    HasChildren =  ns.Kind == SCHEMA_KIND_NAMESPACE
                };
                access.Entry.SetProperty<Display, LocaleString>(ns.GetProperty<Display>()?.Value ?? ns.Name);
            }
            if (ns is Runtime.NamespaceType nt)
            {
                access.Children = nt.GetNodeSchemas().Select(s =>
                {
                    var entry = new Entry<string>
                    {
                        Value = s.FullName,
                        HasChildren = s.Kind == SCHEMA_KIND_NAMESPACE
                    };
                    var display = s.GetProperty<Display>();
                    if (display != null) entry.SetProperty(display);
                    return entry;
                }).ToArray();
            }
            result.Add(access);
            ns = ns.Namespace;
            if (!string.IsNullOrWhiteSpace(root) && root.Equals(ns?.Name, StringComparison.OrdinalIgnoreCase)) break;
        }
        result.Reverse();
        return result;
    }
    
    /// <summary>
    /// Gets the property value type
    /// </summary>
    public static async Task<string?> getproptype(SchemaContext context,
        [Meta<SchemaType>(typeof(Schema.PropertyType))] string name)
    {
        PropertyType? prop = !string.IsNullOrWhiteSpace(name) ? await context.GetNodeTypeAsync<PropertyType>(name) : null;
        return prop?.ValueType?.Name;
    }

    /// <summary>
    /// Gets the sub entries of the value type
    /// </summary>
    public static async Task<List<EntryAccess<string>>> getaccessentries(SchemaContext context,
        [Meta<SchemaType>(typeof(ValueType))] string name,
        string? path = null, string? root = null)
    {
        if (!string.IsNullOrWhiteSpace(root) && !string.IsNullOrWhiteSpace(name) && !name.Equals(root, StringComparison.OrdinalIgnoreCase) && !name.StartsWith($"{root}.", StringComparison.OrdinalIgnoreCase))
            return []; // not access-able
        
        var valueType = !string.IsNullOrWhiteSpace(name) ? await context.GetNodeTypeAsync<Runtime.ValueType>(name) : null;
        if (valueType == null) return [];

        List<EntryAccess<string>> result = [];
        Entry<string>? curr = null;
        while (valueType != null)
        {
            var accessEntry = new EntryAccess<string>();
            Entry<string>[] accesses = valueType.GetAccessEntries().ToArray();
            if (curr != null)
            {
                accessEntry.Entry = new Entry<string> { Value = curr.Value, HasChildren = accesses.Length > 0 };
                accessEntry.Entry.SetProperty<Display, LocaleString>(curr.GetProperty<Display>()?.Value ?? curr.Value);
            }
            accessEntry.Children = accesses;
            
            // check next part
            Runtime.ValueType? next = null;
            foreach (var a in accesses)
            {
                string n = a.Value;
                if (curr != null) a.Value = $"{curr.Value}.{n}";
                if (!string.IsNullOrWhiteSpace(path) && (a.Value.Equals(path, StringComparison.OrdinalIgnoreCase) || 
                                                         path.StartsWith($"{a.Value}.", StringComparison.OrdinalIgnoreCase)))
                {
                    next = valueType.GetAccessValueType(n);
                    curr = a;
                }
            }
            result.Add(accessEntry);
            valueType = next;
        }

        // cut
        if (!string.IsNullOrWhiteSpace(root))
            result = result.SkipWhile(r => (r.Entry?.Value.Length ?? 0) < root.Length).ToList();
        return result;
    }

    /// <summary>
    /// Checks if the schema kind of the schema node with the given name is the same as the given kind
    /// </summary>
    public static async Task<bool> isschemakind(SchemaContext context,
        [Meta<SchemaType>(typeof(AnyType))] string name,
        bool matchArrayElement,
        [Meta<SchemaType>(typeof(SchemaKind))] params string[] kinds)
    {
        var nodeType = string.IsNullOrWhiteSpace(name) ? null : await context.GetNodeTypeAsync(name);
        if (nodeType == null) return false;
        foreach (var kind in kinds)
        {
            if (nodeType.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase) ||
                matchArrayElement && nodeType is ArrayType arr && arr.Element?.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase) == true) 
                return true;
        }
        return false;
    }

    /// <summary>
    /// Checks if the schema kind of the schema node with the given name is a value schema kind
    /// </summary>
    /// <param name="context"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static async Task<bool> isvaluekind(SchemaContext context, [Meta<SchemaType>(typeof(AnyType))] string name)
    {
        var nodeType = string.IsNullOrWhiteSpace(name) ? null : await context.GetNodeTypeAsync(name);
        return nodeType != null && 
            typeof(ValueSchemaKind).GetRecordedValues().Any(v => v.GetValue<string>()!.Equals(nodeType.Kind, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// Checks if the schema kind of the schema node with the given name is a value schema kind and not array schema kind
    /// </summary>
    public static async Task<bool> isarrayele(SchemaContext context, [Meta<SchemaType>(typeof(AnyType))] string name)
    {
        var nodeType = string.IsNullOrWhiteSpace(name) ? null : await context.GetNodeTypeAsync(name);
        return nodeType != null && 
            !nodeType.Kind.Equals(SCHEMA_KIND_ARRAY, StringComparison.OrdinalIgnoreCase) &&
            typeof(ValueSchemaKind).GetRecordedValues().Any(v => v.GetValue<string>()!.Equals(nodeType.Kind, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// The reflection helpers for the schema functions
    /// </summary>
    [Meta<SchemaType>(NS_SYSTEM_SCHEMA_REFLECT_FUNC)]
    public static class Function
    {
        /// <summary>
        /// Checks if the function type's return type match the given type
        /// </summary>
        public static async Task<bool> withreturn(SchemaContext context,
            [Meta<SchemaType>(typeof(FuncType))] string func, 
            [Meta<SchemaType>(typeof(ValueType))] string type,
            bool matchArrayElement = false)
        {
            var nodeType = !string.IsNullOrWhiteSpace(func) ? await context.GetNodeTypeAsync<FunctionType>(func) : null;
            var returnType = !string.IsNullOrWhiteSpace(type) ? await context.GetNodeTypeAsync<Runtime.ValueType>(type) : null;
            return nodeType?.Return != null && returnType != null && (nodeType.Return.IsAssignableTo(returnType) || matchArrayElement && returnType is Runtime.ArrayType { Element: not null } arr && nodeType.Return.IsAssignableTo(arr.Element));
        }

        /// <summary>
        /// Checks if the function type's argument match the given types
        /// </summary>
        public static async Task<bool> withargs(SchemaContext context,
            [Meta<SchemaType>(typeof(AnyType))] string func,
            [Meta<SchemaType>(typeof(ValueType))] params string[] args)
        {
            var funcType = !string.IsNullOrWhiteSpace(func) ? await context.GetNodeTypeAsync<FunctionType>(func) : null;
            if (funcType == null || args.Length != funcType.Args.Length) return false;
            for (int i = 0; i < args.Length; i++)
            {
                var argType = !string.IsNullOrWhiteSpace(args[i]) ? await context.GetNodeTypeAsync<Runtime.ValueType>(args[i]) : null;
                if (argType == null) return false;
                if (funcType.Args[i].ValueType == null || !funcType.Args[i].ValueType!.IsAssignableTo(argType)) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// The reflection helpers for the schema arrays
    /// </summary>
    [Meta<SchemaType>(NS_SYSTEM_SCHEMA_REFLECT_ARRAY)]
    public static class Array
    {
        /// <summary>
        /// Generates the array name for the given element type
        /// </summary>
        public static string genarrayname([Meta<SchemaType>(typeof(ElementType))] string element)
        {
            var split = element.Split('<')[0].Split('.');
            return $"{split[split.Length - 1]}s";
        }

        /// <summary>
        /// Generates the array display name for the given element type
        /// </summary>
        public static string genarraydisplay([Meta<SchemaType>(typeof(ElementType))] string element)
        {
            return $"{Locale.LIST_PREFIX}{{@{element}}}{Locale.LIST_SUFFIX}";
        }
    
        /// <summary>
        /// Gets the array type for the given element type
        /// </summary>
        public static async Task<string> getarraytype(SchemaContext context, [Meta<SchemaType>(typeof(ElementType))] string element)
        {
            var elementType = await context.GetNodeTypeAsync<Runtime.ValueType>(element);
            if (elementType is null) return "";
            return elementType is ArrayType ? elementType.Name : (elementType?.ArrayType?.Name ?? $"{NS_SYSTEM_LIST}<{elementType!.Name}>");
        }

        /// <summary>
        /// Gets the array element type for the given array type
        /// </summary>
        public static async Task<string> getarrayelement(SchemaContext context, [Meta<SchemaType>(typeof(ValueType))] string array)
        {
            var arrayType = await context.GetNodeTypeAsync<Runtime.ValueType>(array);
            if (arrayType is null) return "";
            return arrayType is ArrayType a ? a.Element!.Name : arrayType.Name;
        }
    }

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
            var enumType = await context.GetNodeTypeAsync<Runtime.EnumType>(type);
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
        /// Checks if the enum type has cascades
        /// </summary>
        public static async Task<bool> hascascade(SchemaContext context, [Meta<SchemaType>(typeof(Schema.EnumType))] string type)
        {
            var enumType = await context.GetNodeTypeAsync<Runtime.EnumType>(type);
            return enumType?.Cascade is { Length: > 0 };
        }

        /// <summary>
        /// Gets the cascades for the given enum type
        /// </summary>
        public static async Task<Entry<int>[]> getcascades(SchemaContext context, [Meta<SchemaType>(typeof(Schema.EnumType))] string type)
        {
            var enumType = await context.GetNodeTypeAsync<Runtime.EnumType>(type);
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
            EnumType? enumType = await context.GetNodeTypeAsync<EnumType>(@enum);
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
            EnumType? enumType = await context.GetNodeTypeAsync<EnumType>(@enum);
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
            EnumType? enumType = await context.GetNodeTypeAsync<EnumType>(@enum);
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
            EnumType? enumType = await context.GetNodeTypeAsync<EnumType>(@enum);
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
            EnumType? enumType = await context.GetNodeTypeAsync<EnumType>(@enum);
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
            EnumType? enumType = await context.GetNodeTypeAsync<EnumType>(@enum);
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
            return access.Length > 1 ? access[access.Length - 1].Entry?.Value : null;
        }
    }

    /// <summary>
    /// The reflection helpers for the schema structs
    /// </summary>
    [Meta<SchemaType>(NS_SYSTEM_SCHEMA_REFLECT_STRUCT)]
    public static class Struct;
}