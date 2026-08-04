using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property;
using SchemaNode.Property.Core;
using SchemaNode.Property.Record;
using SchemaNode.Schema;
using SchemaNode.Utility;
using ValueType = SchemaNode.Schema.ValueType;
using static SchemaNode.Utility.Constant;

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
        var split = element.Split('<')[0].Split('.');
        return $"{split[^1]}s";
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
        return (elementType?.ArrayType?.Name ?? $"{NS_SYSTEM_LIST}<{elementType!.Name}>");
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
}