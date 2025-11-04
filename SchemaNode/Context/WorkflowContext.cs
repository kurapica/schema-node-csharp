namespace SchemaNode.Context;

/// <summary>
/// The workflow context
/// </summary>
public class WorkflowContext(IServiceProvider serviceProvider): SchemaContext(serviceProvider)
{
    #region Properties

    public Guid WorkflowId { get; set; }

    #endregion
}