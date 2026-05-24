using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property;
using SchemaNode.Property.Core;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;
using ValueSchemaKind = SchemaNode.Property.Record.ValueSchemaKind;
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace SchemaNode.Function;

[Meta<SchemaType>(NS_SYSTEM_SCHEMA_REFLECT)]
public static class SystemReflect
{
    public static async Task<bool> isvaluetype(SchemaContext context, [Meta<SchemaType>(typeof(AnyType))] string name)
    {
        var nodeType = string.IsNullOrWhiteSpace(name) ? null : await context.GetNodeTypeAsync(name);
        return nodeType != null && 
            typeof(ValueSchemaKind).GetRecordedValues().Any(v => v.GetValue<string>()!.Equals(nodeType.Kind, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// Checks if the schema kind of the schema node with the given name is the same as the given kind
    /// </summary>
    public static async Task<bool> isschemakind(SchemaContext context, 
        [Meta<SchemaType>(typeof(AnyType))] string name, 
        [Meta<SchemaType>(typeof(SchemaKind))] string kind)
    {
        var nodeType = string.IsNullOrWhiteSpace(name) ? null : await context.GetNodeTypeAsync(name);
        return nodeType?.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    public static async Task<bool> isarrayele(SchemaContext context, [Meta<SchemaType>(typeof(AnyType))] string name)
    {
        var nodeType = string.IsNullOrWhiteSpace(name) ? null : await context.GetNodeTypeAsync(name);
        return nodeType != null && 
            !nodeType.Kind.Equals(SCHEMA_KIND_ARRAY, StringComparison.OrdinalIgnoreCase) &&
            typeof(ValueSchemaKind).GetRecordedValues().Any(v => v.GetValue<string>()!.Equals(nodeType.Kind, StringComparison.OrdinalIgnoreCase));
    }
}