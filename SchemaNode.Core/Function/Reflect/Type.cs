using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Schema;
using SchemaNode.Struct;
using static SchemaNode.Utility.Constant;
using ValueType = SchemaNode.Schema.ValueType;

namespace SchemaNode.Function.Reflect;

[Meta<SchemaType>(NS_SYSTEM_SCHEMA_REFLECT_TYPE)]
public static class Type
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
    /// Gets the sub entries of the value type
    /// </summary>
    public static async Task<List<EntryAccess<string>>> getaccessentries(SchemaContext context,
        [Meta<SchemaType>(typeof(ValueType))] string name,
        string? path = null, string? root = null)
    {
        if (!string.IsNullOrWhiteSpace(root) && !string.IsNullOrWhiteSpace(path) && !path.Equals(root, StringComparison.OrdinalIgnoreCase) && !path.StartsWith($"{root}.", StringComparison.OrdinalIgnoreCase))
            return []; // not access-able
        path ??= root;
        
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
                matchArrayElement && nodeType is Runtime.ArrayType arr && arr.Element?.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase) == true) 
                return true;
        }
        return false;
    }

    /// <summary>
    /// Gets the schema kind of the schema node with the given name
    /// </summary>
    public static async Task<string?> gettypekind(SchemaContext context, [Meta<SchemaType>(typeof(AnyType))] string name)
    {
        var nodeType = string.IsNullOrWhiteSpace(name) ? null : await context.GetNodeTypeAsync(name);
        return nodeType?.Kind;
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
    /// Checks if the type is assignable to other value type
    /// </summary>
    public static async Task<bool> isassignableto(SchemaContext context, [Meta<SchemaType>(typeof(ValueType))] string type, [Meta<SchemaType>(typeof(ValueType))] string target)
    {
        var typeNode = string.IsNullOrWhiteSpace(type) ? null : await context.GetNodeTypeAsync<Runtime.ValueType>(type);
        var targetNode = string.IsNullOrWhiteSpace(target) ? null : await context.GetNodeTypeAsync<Runtime.ValueType>(target);
        if (typeNode == null || targetNode == null) return false;
        return typeNode.IsAssignableTo(targetNode);
    }
}