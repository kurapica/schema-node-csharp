using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchemaNode.Runtime;

namespace SchemaNode.Context;

/// <summary>
/// The schema context
/// </summary>
public class SchemaContext(IServiceProvider serviceProvider, ISchemaRunTime runTime): ISchemaContext, IDisposable
{
    #region Fields
    
    /// <summary>
    /// The schema provider
    /// </summary>
    public IServiceProvider ServiceProvider { get; } = serviceProvider;
    
    /// <summary>
    /// The schema runtime
    /// </summary>
    public ISchemaRunTime RunTime { get; } = runTime;

    /// <summary>
    /// Gets the logger
    /// </summary>
    ILogger Logger => _loggerThunk.Value;
    readonly Lazy<ILogger> _loggerThunk = new(serviceProvider.GetRequiredService<ILogger<SchemaContext>>);

    #endregion

    #region Log

    /// <summary>
    /// Log debug message
    /// </summary>
    public void LogDebug(string message, params object?[] args) => Logger.LogDebug(message, args);
    
    /// <summary>
    /// Log information message
    /// </summary>
    public void LogInformation(string message, params object?[] args) => Logger.LogInformation(message, args);
    
    /// <summary>
    /// Log warning message
    /// </summary>
    public void LogWarning(string message, params object?[] args) => Logger.LogWarning(message, args);
    
    /// <summary>
    /// Log error message
    /// </summary>
    public void LogError(Exception ex, string message, params object?[] args) => Logger.LogError(ex, message, args);

    #endregion
    
    #region Implementation of IDisposable

    public void Dispose()
    {
    }

    #endregion
    
}
