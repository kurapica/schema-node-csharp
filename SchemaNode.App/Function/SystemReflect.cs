using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Event;
using SchemaNode.Property.Core;
using SchemaNode.Scalar;
using SchemaNode.Schema;
using SchemaNode.Struct;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
// ReSharper disable InconsistentNaming

namespace SchemaNode.Function;

/// <summary>
/// The system reflect
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_SCHEMA_REFLECT)]
public static class SystemAppReflect
{
    public static async Task<Entry<string>[]> getapps(SchemaContext context,
        [Meta<SchemaType>(typeof(AppType))] string? root = null)
    {
        await Task.Yield();
        return [];
    }

    public static async Task<Entry<string>[]> getappfields(SchemaContext context,
        [Meta<SchemaType>(typeof(AppType))] string app)
    {
        await Task.Yield();
        return [];
    }
    
    public static async Task<string?> getappfieldtype(SchemaContext context,
        [Meta<SchemaType>(typeof(AppType))] string app,
        [Meta<SchemaType>(typeof(Identifier))] string field,
        bool elementType = false)
    {
        await Task.Yield();
        return null;
    }

    [Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_REFLECT}.event")]
    public static class Event
    {
        /// <summary>
        /// Get app field data chagne event payload type
        /// </summary>
        public static async Task<string> getappfieldpayload(SchemaContext context,
            [Meta<SchemaType>(typeof(AppType))] string? app,
            [Meta<SchemaType>(typeof(Identifier))] string? field)
        {
            return await getappfieldtype(context, app!, field!, true) ?? string.Empty;
        }

        /// <summary>
        /// Get app field data update event payload type
        /// </summary>
        public static async Task<string> getappfieldupdatepayload(SchemaContext context,
            [Meta<SchemaType>(typeof(AppType))] string? app,
            [Meta<SchemaType>(typeof(Identifier))] string? field)
        {
            var item = await getappfieldtype(context, app!, field!, true);
            if (string.IsNullOrWhiteSpace(item)) return string.Empty;
            return typeof(AppFieldUpdatePayload).GetSchemaType()! + $"<{item}>";
        }
    }
    
    [Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_REFLECT}.workflow")]
    public static class Workflow
    {
        /// <summary>
        /// Checks the given workflow is of the given kind
        /// </summary>
        public static async Task<bool> iskind(SchemaContext context,
            [Meta<SchemaType>(typeof(WorkflowType))] string workflow,
            [Meta<SchemaType>(typeof(WorkflowKind))] string kind)
        {
            var workflowType = await context.GetNodeTypeAsync<Runtime.WorkflowType>(workflow);
            return kind.Equals(workflowType?.WorkflowKind, StringComparison.OrdinalIgnoreCase);
        }
    }
}
