using SchemaNode.Runtime;

namespace SchemaNode.Components;

/// <summary>
/// The interaction workflow
/// </summary>
public abstract class InteractionWorkflow: Workflow
{
    /// <summary>
    /// The form type if not the payload type not match
    /// </summary>
    public AnySchemeType? FormType { get; set; }
}