namespace SchemaNode.Components;

public interface IWorkflowEventScheduler
{
    void Schedule<T>(WorkflowEvent<T> workflowEvent);
}