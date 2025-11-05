using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Enum;

namespace SchemaNode.Components;

/// <summary>
/// The workflow scope event
/// </summary>
public abstract class WorkflowEvent: Event
{
    /// <summary>
    /// The workflow identifier
    /// </summary>
    public Guid WorkflowId { get; set; }
}