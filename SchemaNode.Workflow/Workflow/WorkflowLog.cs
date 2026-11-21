using SchemaNode.Context;
using SchemaNode.Enum.Workflow;
using System.Text.Json.Nodes;

namespace SchemaNode.Workflow;

/// <summary>
/// The workflow step log data
/// </summary>
public record WorkflowLog(WorkflowContext Context, BaseWorkflow? Step, WorkflowStatus Status, JsonNode? Message = null);