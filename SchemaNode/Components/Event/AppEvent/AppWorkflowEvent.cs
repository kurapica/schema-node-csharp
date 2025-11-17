namespace SchemaNode.Components;

/// <summary>
/// The base class for all workflow-related application events.
/// </summary>
public abstract class AppWorkflowEvent(string app): AppEvent(app)
{
}