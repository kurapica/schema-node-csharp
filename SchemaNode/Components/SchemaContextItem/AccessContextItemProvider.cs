using SchemaNode.Context;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Components;

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
}

/// <summary>
/// The access context item provider extensions
/// </summary>
public static class AccessContextItemProviderExtensions
{
    /// <summary>
    /// Set the access information
    /// </summary>
    public static void SetAccess(this SchemaContext context, string? app = null, string? target = null)
    {
        // Clear the policy evaluation cache
        context.GetOrCreateContextItem<PolicyEvaluatorResult>().Result.Clear();
        
        // Gets the shared access
        var access = context.GetRequiredService<Access>();
        access.App = app;
        access.Target = target;
    }
}