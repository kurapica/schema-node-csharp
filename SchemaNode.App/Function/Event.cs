using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Event;
using SchemaNode.Property.Core;
using SchemaNode.Scalar;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Function;


[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_REFLECT}.event")]
public static class SystemReflectEvent
{
    /// <summary>
    /// Get app field data change event payload type
    /// </summary>
    public static async Task<string> getappfieldpayload(SchemaContext context,
        [Meta<SchemaType>(typeof(AppType))] string? app,
        [Meta<SchemaType>(typeof(Identifier))] string? field)
    {
        return await SystemReflectApp.getappfieldtype(context, app!, field!, true) ?? string.Empty;
    }

    /// <summary>
    /// Get app field data update event payload type
    /// </summary>
    public static async Task<string> getappfieldupdatepayload(SchemaContext context,
        [Meta<SchemaType>(typeof(AppType))] string? app,
        [Meta<SchemaType>(typeof(Identifier))] string? field)
    {
        var item = await SystemReflectApp.getappfieldtype(context, app!, field!, true);
        if (string.IsNullOrWhiteSpace(item)) return string.Empty;
        return typeof(AppFieldUpdatePayload).GetSchemaType()! + $"<{item}>";
    }
}