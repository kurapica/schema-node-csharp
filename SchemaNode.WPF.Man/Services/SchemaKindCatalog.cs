using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property.Common;
using SchemaNode.Property.Property;
using SchemaType = SchemaNode.Property.Core.SchemaType;
using SchemaNode.Property.Struct;
using SchemaNode.Runtime;
using SchemaNode.Struct;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.WPF.Man.Services;

/// <summary>
/// Kind domain for the definition side.
///
/// Faithfully replicates <c>handleNew(copySchema?)</c> from schema-node-man/src/components/schema.vue:
///   1. Read all available kinds from the <c>system.schema.node.kind</c> enum;
///   2. Initially allow only <see cref="NODE_KIND_NAMESPACE"/>;
///   3. Iterate the property types of <see cref="SCHEMA_KIND_NODE"/>:
///      - Skip properties whose <c>ReadOnly</c> flag is already set;
///      - The property must declare <c>PropertyValueType</c>;
///      - That value type must be loadable as a <c>StructType</c>;
///      - The second segment of its <c>Attach</c> property (the kind in <c>{namespace}.{kind}</c>) exists in the enum set;
///   4. The generated result becomes the <c>WhiteList</c> of the new node's <c>kind</c> field.
///
/// Note: this whitelist is only used for the domain of "new schema"; it does not take part in render dispatch;
/// rendering is determined by the type/kind registrations in <c>SchemaViewRegistry</c>.
/// </summary>
public static class SchemaKindCatalog
{
    /// <summary>
    /// Computes the available set for the <c>kind</c> field when creating a new schema (including the initial namespace).
    /// </summary>
    public static async Task<string[]> GetNodeKindsAsync(SchemaContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // 1. Enumerate all kinds defined by the enum
        EnumType? kindEnum = await context.GetNodeTypeAsync<EnumType>($"{NS_SYSTEM_SCHEMA_NODE}.kind");
        EntryAccess<string>[] access = kindEnum == null
            ? []
            : await kindEnum.GetEnumEntryAccessAsync(context, null);
        string[] nodeKinds = access.Length > 0
            ? (access[0].Children ?? []).Select(c => c.Value).ToArray()
            : [];

        // 2. Initially allow only namespace (matching the initial whiteList value in handleNew)
        List<string> whiteList = [NODE_KIND_NAMESPACE];

        // 3. Filter addable node kinds by the property types of the schema kind
        foreach (Type propCtor in context.Runtime.GetSchemaKindPropertyTypes(SCHEMA_KIND_NODE))
        {
            // Read-only properties cannot be added
            if (propCtor.GetMetaProperty<ReadOnly>() is { HasValue: true, Value: true })
                continue;

            string? typeName;
            if (propCtor.GetMetaProperty<PropertyValueType>() is { HasValue: true } valueType)
            {
                typeName = valueType.GetValue<string>();
            }
            else
            {
                // The kind define properties (IntProperty, StructProperty, ...) declare no
                // PropertyValueType meta; the define schema type is carried by the Property<T>
                // generic argument instead, and its SchemaType meta is the loadable type name.
                Type? schemaArg = propCtor.BaseType is { IsGenericType: true } baseType
                    ? baseType.GetGenericArguments()[0]
                    : null;
                typeName = schemaArg?.GetMetaProperty<SchemaType>()?.Value;
            }
            if (string.IsNullOrWhiteSpace(typeName))
                continue;

            NodeType? nodeType = await context.GetNodeTypeAsync(typeName);
            if (nodeType is not StructType)
                continue;

            // The Attach property on StructType is the node schema kind
            Attach? attach = nodeType.GetProperty<Attach>();
            if (attach is not { HasValue: true })
                continue;

            string? attachValue = attach.GetValue<string>();
            if (attachValue is null)
                continue;

            // attach may declare multiple kinds, separated by comma or semicolon,
            // e.g. "node.int.define,node.string.define"
            foreach (string raw in attachValue.Split(new[] { ',', ';' },
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] parts = raw.Split('.', StringSplitOptions.RemoveEmptyEntries);
                string kind = parts.Length >= 2 ? parts[1] : parts.ElementAtOrDefault(0) ?? string.Empty;
                if (kind.Length > 0 && nodeKinds.Contains(kind) &&
                    !whiteList.Any(k => k.Equals(kind, StringComparison.OrdinalIgnoreCase)))
                    whiteList.Add(kind);
            }
        }

        return whiteList.ToArray();
    }
}
