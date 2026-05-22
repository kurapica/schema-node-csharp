using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Service;

namespace SchemaNode.App.Service;

/// <summary>
/// App-layer runtime stage handler.
/// Registers App-specific behavior into the Core schema pipeline without modifying Core.
/// Handles App type activation / deactivation and workflow startup.
/// </summary>
public sealed class AppRuntimeStageHandler : IRuntimeStageHandler
{
    /// <inheritdoc />
    public async Task OnActivatingAsync(ISchemaContext context)
    {
        ILogger logger = context.Services.GetRequiredService<ILogger<AppRuntimeStageHandler>>();

        // Retrieve all registered App type managers and trigger pre-load
        foreach (IAppTypeManager manager in context.Services.GetServices<IAppTypeManager>())
        {
            try
            {
                logger.LogInformation("[AppRuntimeStageHandler] Activating app types via {manager}", manager.GetType().Name);
                await manager.PreloadAsync(context);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[AppRuntimeStageHandler] Error activating App type manager {manager}", manager.GetType().Name);
            }
        }
    }

    /// <inheritdoc />
    public async Task OnDeactivatingAsync(ISchemaContext context)
    {
        foreach (IAppTypeManager manager in context.Services.GetServices<IAppTypeManager>())
        {
            try
            {
                await manager.DeactivateAsync(context);
            }
            catch (Exception ex)
            {
                ILogger logger = context.Services.GetRequiredService<ILogger<AppRuntimeStageHandler>>();
                logger.LogError(ex, "[AppRuntimeStageHandler] Error deactivating App type manager {manager}", manager.GetType().Name);
            }
        }
    }
}
