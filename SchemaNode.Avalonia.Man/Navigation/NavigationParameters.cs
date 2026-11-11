namespace SchemaNode.Avalonia.Man.Navigation;

/// <summary>
/// Parameter bag passed between navigation targets. Mirrors the Prism type the view models were
/// written against, so the port keeps the same call sites (`new NavigationParameters { { key, value } }`).
/// </summary>
public class NavigationParameters : Dictionary<string, object?>
{
    /// <summary>Creates an empty parameter bag.</summary>
    public NavigationParameters()
    {
    }

    /// <summary>Creates a parameter bag from key/value pairs.</summary>
    public NavigationParameters(IEnumerable<KeyValuePair<string, object?>> values)
        : base(values)
    {
    }

    /// <summary>Reads a strongly typed parameter; returns null when absent or of another type.</summary>
    public T? GetValue<T>(string key)
    {
        if (!TryGetValue(key, out object? value)) return default;
        return value is T typed ? typed : default;
    }
}

/// <summary>Navigation request context. Only <see cref="NavigationParameters"/> is used by this app.</summary>
public sealed class NavigationContext
{
    /// <summary>Creates a context for the supplied parameters.</summary>
    public NavigationContext(NavigationParameters parameters) => Parameters = parameters;

    /// <summary>Parameters supplied by the navigation caller.</summary>
    public NavigationParameters Parameters { get; }
}

/// <summary>Implemented by view models that need to react to navigation.</summary>
public interface INavigationAware
{
    /// <summary>Called after the target was created and before it is displayed.</summary>
    void OnNavigatedTo(NavigationContext navigationContext);

    /// <summary>Whether an existing instance can serve the request. Single-view shell: unused.</summary>
    bool IsNavigationTarget(NavigationContext navigationContext);

    /// <summary>Called when another target replaces this one.</summary>
    void OnNavigatedFrom(NavigationContext navigationContext);
}

/// <summary>Implemented by view models that release resources when navigation leaves them.</summary>
public interface IDestructible
{
    /// <summary>Releases owned wrappers, nodes, contexts and scopes.</summary>
    void Destroy();
}

/// <summary>Outcome of a navigation request.</summary>
public interface INavigationResult
{
    /// <summary>Whether navigation completed.</summary>
    bool Success { get; }

    /// <summary>Failure reason when <see cref="Success"/> is false.</summary>
    Exception? Exception { get; }

    /// <summary>Context the target was navigated with.</summary>
    NavigationContext? Context { get; }
}

/// <summary>Default <see cref="INavigationResult"/> implementation.</summary>
public sealed class NavigationResult : INavigationResult
{
    /// <summary>Creates a result.</summary>
    public NavigationResult(Exception? exception, NavigationContext? context)
    {
        Exception = exception;
        Context = context;
    }

    /// <inheritdoc />
    public bool Success => Exception is null;

    /// <inheritdoc />
    public Exception? Exception { get; }

    /// <inheritdoc />
    public NavigationContext? Context { get; }
}
