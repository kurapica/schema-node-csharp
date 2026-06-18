using SchemaNode.Property.App;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Context;

/// <summary>
/// The access context item provider
/// </summary>
public class AccessContextItemProvider(Access access): ISchemaContextItemProvider<Access>
{
    public bool HasItem => true;
    
    public Access GetItem() => access;
}

/// <summary>
/// The access information
/// </summary>
public class Access
{
    /// <summary>
    /// The access application
    /// </summary>
    public string? App { get; set; }

    /// <summary>
    /// The access target
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// Sets the access information, and will clear the stack and reset to the new state
    /// </summary>
    internal void SetAccess(string? app = null, string? target = null)
    {
        App = app;
        Target = target;
        _stack.Clear();
    }

    internal bool Stack(string? app = null, string? target = null)
    {
        bool hasChange = App != app || Target != target;
        _stack.Push((App, Target));
        App = app;
        Target = target;
        return hasChange;
    }
    
    internal bool Unstack()
    {
        if (_stack.Count > 0)
        {
            var (app, target) = _stack.Pop();
            bool hasChange = App != app || Target != target;
            App = app;
            Target = target;
            return hasChange;
        }
        else
        {
            App = null;
            Target = null;
            return true;
        }
    }

    private readonly Stack<(string? App, string? Target)> _stack = new ();
}

public sealed class AccessScope(SchemaContext context) : IDisposable
{
    public void Dispose()
    {
        var access = context.GetRequiredService<Access>();
        if (access.Unstack())
            context.GetOrAddContextItem<PolicyEvaluatorResult>().Result.Clear();
    }
}

/// <summary>
/// The access context item provider extensions
/// </summary>
public static class AccessContextItemProviderExtensions
{
    public static void SetAccess(this SchemaContext context, Access access)
    {
        // Clear the policy evaluation cache
        context.GetOrAddContextItem<PolicyEvaluatorResult>().Result.Clear();
        
        // Gets the shared access
        var sharedAccess = context.GetRequiredService<Access>();
        sharedAccess.SetAccess(access.App, access.Target);
    }

    /// <summary>
    /// Set the access information
    /// </summary>
    public static void SetAccess(this SchemaContext context, string? app = null, string? target = null)
    {
        // Clear the policy evaluation cache
        context.GetOrAddContextItem<PolicyEvaluatorResult>().Result.Clear();
        
        // Gets the shared access
        var access = context.GetRequiredService<Access>();
        access.SetAccess(app, target);
    }
    
    /// <summary>
    /// Stack the access information, and will be unstacked when the context is disposed
    /// </summary>
    /// <param name="context"></param>
    /// <param name="app"></param>
    /// <param name="target"></param>
    public static AccessScope StackAccess(this SchemaContext context, string? app = null, string? target = null)
    {
        // Gets the shared access
        var access = context.GetRequiredService<Access>();
        
        // Clear the policy evaluation cache if changed
        if(access.Stack(app, target))
            context.GetOrAddContextItem<PolicyEvaluatorResult>().Result.Clear();
            
        return new AccessScope(context);
    }
}