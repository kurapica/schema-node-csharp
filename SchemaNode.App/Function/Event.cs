using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Function;

[Meta<SchemaType>(NS_SYSTEM_SCHEMA_REFLECT_EVENT)]
public static class SystemReflectEvent
{
    /// <summary>
    /// The event type has arguments
    /// </summary>
    public static async Task<bool> hasargs(SchemaContext context,
        [Meta<SchemaType>(typeof(Schema.EventType))] string @event)
    {
        var eventType = !string.IsNullOrWhiteSpace(@event) ? await context.GetNodeTypeAsync<Runtime.EventType>(@event) : null;
        return eventType != null && eventType?.Args?.Length > 0;
    }

    /// <summary>
    /// Checks if the function type's return type match the given type
    /// </summary>
    public static async Task<bool> withpayload(SchemaContext context,
        [Meta<SchemaType>(typeof(Schema.EventType))] string @event,
        [Meta<SchemaType>(typeof(Schema.ValueType))] string? type = null)
    {
        var returnType = !string.IsNullOrWhiteSpace(type) ? await context.GetNodeTypeAsync<Runtime.ValueType>(type) : null;
        if (returnType == null) return true;
        var nodeType = !string.IsNullOrWhiteSpace(@event) ? await context.GetNodeTypeAsync<Runtime.EventType>(@event) : null;
        if (nodeType == null) return false;
        return nodeType?.Payload != null && returnType != null && (nodeType.Payload.IsAssignableTo(returnType));
    }
}
