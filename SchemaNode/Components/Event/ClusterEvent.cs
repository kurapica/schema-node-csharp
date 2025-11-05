using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Runtime;

namespace SchemaNode.Components;

/// <summary>
/// The cluster scope event
/// </summary>
public abstract class ClusterEvent: Event
{
}