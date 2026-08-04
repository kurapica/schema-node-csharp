using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Schema;
using SchemaNode.Struct;
using static SchemaNode.Utility.Constant;
using ValueType = System.ValueType;

namespace SchemaNode.Function.Reflect;


/// <summary>
/// The reflection helpers for the schema structs
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_SCHEMA_REFLECT_STRUCT)]
public static class Struct
{
    /// <summary>
    /// Gets the sub entries of the value type
    /// </summary>
    public static async Task<List<EntryAccess<string>>> getaccessentries(SchemaContext context,
        StructFieldSchema[]  fields,
        string? path = null,
        string? root = null)
    {
        if (!string.IsNullOrWhiteSpace(root) && !string.IsNullOrWhiteSpace(path) && !path.Equals(root, StringComparison.OrdinalIgnoreCase) && !path.StartsWith($"{root}.", StringComparison.OrdinalIgnoreCase))
            return []; // not access-able
        path ??= root;
        
        List<Entry<string>> first = [];
        foreach (StructFieldSchema f in fields.Where(f => !string.IsNullOrWhiteSpace(f.Name) && !string.IsNullOrWhiteSpace(f.Type)))
        {
            Runtime.ValueType? fieldType = await context.GetNodeTypeAsync<Runtime.ValueType>(f.Type);
            if (fieldType == null) continue;
            var entry = new Entry<string> { Value = f.Name, HasChildren = fieldType.HasAccessEntries };
            entry.SetProperty<Display, LocaleString>(f.GetProperty<Display>()?.Value ?? fieldType.GetProperty<Display>()?.Value ?? f.Name);
            first.Add(entry);
        }
        
        // build the access entries
        List<EntryAccess<string>> result = [new (){ Children = first.ToArray() }];
        Entry<string>? curr = !string.IsNullOrWhiteSpace(path) ? result[0].Children!
            .FirstOrDefault(c => path.Equals(c.Value, StringComparison.OrdinalIgnoreCase) ||
                                 path.StartsWith($"{c.Value}.", StringComparison.OrdinalIgnoreCase)) : null;
        Runtime.ValueType? valueType = curr != null 
            ? await context.GetNodeTypeAsync<Runtime.ValueType>(fields.First(f => f.Name.Equals(curr.Value, StringComparison.OrdinalIgnoreCase)).Type) 
            : null;
        
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
                if (!string.IsNullOrWhiteSpace(path) && (path.Equals(a.Value, StringComparison.OrdinalIgnoreCase) || 
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
}