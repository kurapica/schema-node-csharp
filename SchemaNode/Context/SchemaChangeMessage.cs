using SchemaNode.Components;

namespace SchemaNode.Context;

public class SchemaChangeMessage
{
    /// <summary>
    /// The changed schemas
    /// </summary>
    public string[]? Schemas { get; set; }

    /// <summary>
    /// The deleted schemas
    /// </summary>
    public string[]? DeleteSchemas { get; set; }
    
    /// <summary>
    /// The changed apps
    /// </summary>
    public string[]? Apps { get; set; } 
    
    /// <summary>
    /// The deleted apps
    /// </summary>
    public string[]? DeleteApps { get; set; }
}

/// <summary>
/// Reload types
/// </summary>
public class SchemaChangeMessageHandler : ISchemaMessageHandler<SchemaChangeMessage>
{
    public async Task HandleAsync(SchemaContext context, SchemaChangeMessage message)
    {
        if (message.Schemas != null)
            foreach (string schema in message.Schemas)
                await context.GetSchemaNodeAsync(schema, reload: true).ConfigureAwait(false);
        
        if (message.DeleteSchemas != null)
            foreach (string schema in message.DeleteSchemas)
                context.RemoveSchemaNode(schema);
        
        if (message.Apps != null)
            foreach (string app in message.Apps)
                await context.GetAppNodeAsync(app, reload: true).ConfigureAwait(false);
            
        if (message.DeleteApps != null)
            foreach (string app in message.DeleteApps)
                context.RemoveAppNode(app);
    }
}