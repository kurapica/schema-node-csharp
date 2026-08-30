using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Record;
using SchemaNode.Schema;
using SchemaNode.Struct;
using SchemaNode.Utility;
using ValueType = SchemaNode.Schema.ValueType;
using static SchemaNode.Utility.Constant;
// ReSharper disable InconsistentNaming

namespace SchemaNode.Function.Reflect;

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
        if (string.IsNullOrWhiteSpace(element)) return "";
        var split = element.Split('<')[0].Split('.');
        return $"{split[^1]}s";
    }

    /// <summary>
    /// Generates the array display name for the given element type
    /// </summary>
    public static string genarraydisplay([Meta<SchemaType>(typeof(ElementType))] string element)
    {
        if (string.IsNullOrWhiteSpace(element)) return "";
        return $"{Locale.LIST_PREFIX}{{@{element}}}{Locale.LIST_SUFFIX}";
    }
    
    /// <summary>
    /// Gets the array type for the given element type
    /// </summary>
    public static async Task<string> getarraytype(SchemaContext context, [Meta<SchemaType>(typeof(ElementType))] string element)
    {
        var elementType = await context.GetNodeTypeAsync<Runtime.ValueType>(element);
        if (elementType is null) return "";
        return (elementType.ArrayType?.Name ?? $"{NS_SYSTEM_LIST}<{elementType.Name}>");
    }

    /// <summary>
    /// Gets the array element type for the given array type
    /// </summary>
    public static async Task<string> getarrayelement(SchemaContext context, [Meta<SchemaType>(typeof(ValueType))] string array)
    {
        var arrayType = await context.GetNodeTypeAsync<Runtime.ValueType>(array);
        if (arrayType is null) return "";
        return arrayType is Runtime.ArrayType a ? a.Element!.Name : arrayType.Name;
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
    /// Gets the sub entries of the value type
    /// </summary>
    public static async Task<List<EntryAccess<string>>> getaccessentries(SchemaContext context,
        [Meta<SchemaType>(typeof(ValueType))] string element,
        string? path = null, string? root = null)
    {
        if (!string.IsNullOrWhiteSpace(root) && !string.IsNullOrWhiteSpace(path) && !path.Equals(root, StringComparison.OrdinalIgnoreCase) && !path.StartsWith($"{root}.", StringComparison.OrdinalIgnoreCase))
            return []; // not access-able
        path ??= root;
        
        // the first entry list
        List<Entry<string>> first = [
            new Entry<string> { Value = ARRAY_PREVIOUS },
            new Entry<string> { Value = ARRAY_ELEMENT }
        ];
        var elementType = await context.GetNodeTypeAsync<Runtime.ValueType>(element);
        if (elementType == null) return [];
        foreach (Entry<string> a in elementType.GetAccessEntries())
            first.Add(a);
        
        // build the access entries
        List<EntryAccess<string>> result = [new (){ Children = first.ToArray() }];
        Entry<string>? curr = !string.IsNullOrWhiteSpace(path) ? result[0].Children!.Skip(2)
            .FirstOrDefault(c => c.Value.Equals(path, StringComparison.OrdinalIgnoreCase) ||
                                 c.Value.StartsWith($"{path}.", StringComparison.OrdinalIgnoreCase)) : null;
        Runtime.ValueType? valueType = curr != null ? elementType.GetAccessValueType(curr.Value) : null;
        
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
    /// Gets the access value type
    /// </summary>
    public static async Task<string?> getaccessvaluetype(SchemaContext context,  [Meta<SchemaType>(typeof(ValueType))] string element, string? path = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var elementType = await context.GetNodeTypeAsync<Runtime.ValueType>(element);
        if (elementType is null) return null;
        if (path.Equals(NODE_SELF, StringComparison.OrdinalIgnoreCase) || path.Equals(ARRAY_PREVIOUS, StringComparison.OrdinalIgnoreCase)) return $"{NS_SYSTEM_LIST}<{elementType.Name}>";
        return path.Equals(ARRAY_ELEMENT, StringComparison.OrdinalIgnoreCase) ? elementType.Name : elementType.GetAccessValueType(path)?.Name;
    }
}