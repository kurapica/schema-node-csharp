namespace SchemaNode.Attribute;

/// <summary>
/// Indicate the function can only be used in workflow
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class WorkflowOnlyAttribute: System.Attribute
{
}